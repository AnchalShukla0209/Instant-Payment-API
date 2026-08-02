using InstantPay.Application.Interfaces;
using InstantPay.Application.Interfaces.MoneyTransfer.Finzep;
using InstantPay.Application.Interfaces.SMS;
using InstantPay.Infrastructure.Sql.Entities;
using InstantPay.SharedKernel.Entity.FinzepConfigDTO;
using InstantPay.SharedKernel.RequestPayload.MoneyTransfer.Finzep;
using InstantPay.SharedKernel.Results;
using InstantPay.SharedKernel.Results.MoneyTransfer.Castler;
using InstantPay.SharedKernel.Results.MoneyTransfer.Finzep;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace InstantPay.Application.Services.MoneyTransfer
{
    public class FinzepDmtService : IFinzepDmtService
    {
        private readonly FinzepConfig _config;
        private readonly AppDbContext _context;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IWalletService _walletService;
        private readonly ICommissionService _commissionService;
        private readonly ISmsService _smsService;

        private const string PayoutUrl = "https://api.finzep.com/API/Payout";
        private const string StatusCheckUrl = "https://api.finzep.com/API/StatusCheck";

        private const int ServiceId = 6;
        private const string ApiCode = "FZP";

        public FinzepDmtService(IOptions<FinzepConfig> config, AppDbContext context, IHttpClientFactory httpClientFactory, IWalletService walletService, ICommissionService commissionService, ISmsService smsService)
        {
            _config = config.Value;
            _context = context;
            _httpClientFactory = httpClientFactory;
            _walletService = walletService;
            _commissionService = commissionService;
            _smsService = smsService;
        }

        private static string Truncate(string input, int maxLen)
        {
            if (string.IsNullOrEmpty(input)) return string.Empty;
            return input.Length <= maxLen ? input : input.Substring(0, maxLen);
        }

        public bool VerifyPin(string inputPin, string txnPin)
        {
            return inputPin.ToLower().Trim() == txnPin.ToLower().Trim();
        }

        private static string MapStatus(int finzepStatus)
        {
            return finzepStatus switch
            {
                2 => "SUCCESS",
                1 => "PENDING",
                3 => "FAILED",
                4 => "REFUNDED",
                _ => "PENDING"
            };
        }

        private Task<decimal> GetCommissionFromPlanAsync(
            int planId, decimal amount, string shareColumn)
            => _commissionService.GetCommissionFromPlanAsync(planId, amount, ServiceId, ApiCode, shareColumn);

        private Task DistributeCommissionAsync(
            TransactionDetail tx, TblUser user, decimal amount, int planId)
            => _commissionService.DistributeCommissionAsync(
                tx, user, amount, planId, ServiceId, ApiCode,
                $"Commission Credit DMT Payment For Account No {tx.AccountNo}| Credit by Services");

        private async Task RefundUserAsync(TransactionDetail tx)
        {
            var user = await _context.TblUsers.FirstOrDefaultAsync(x => x.Id == Convert.ToInt32(tx.UserId));
            if (user == null) return;

            decimal rtComm = Convert.ToDecimal(tx.Charge);
            decimal totalRefund = Convert.ToDecimal(tx.Amount) + rtComm;

            await _walletService.CreditAsync(
                user.Id, user.Username + "-" + user.Phone,
                tx.Amount ?? 0, totalRefund, rtComm, 0,
                "Money_Transfer_Refund",
                $"Money Transfer Refunded | DMT Payment For Account No {tx.AccountNo}| Credit by Services | Refund Credit TXN:{tx.TxnId}",
                user.Wlid);
        }

        public async Task<LoginModel> MoneyTransfer(FinzepDmtRequest model, string ip, CancellationToken cancellationToken)
        {
            try
            {
                if(model?.BankName?.ToUpper().Trim() == "AIRTEL PAYMENTS BANK")
                {
                    return Fail("Service Down for AIRTEL PAYMENTS BANK Please try with other Bank Name");
                }
                var user = await _context.TblUsers.FirstOrDefaultAsync(x => x.Id == Convert.ToInt32(model.UserId), cancellationToken);
                if (user == null)
                    return Fail("User not found");

                if (!VerifyPin(model.TransactionPin, user.TxnPin))
                    return Fail("Invalid Pin");

                decimal rtComm = 0m;
                decimal totalDebit = 0m;
                decimal newBal = 0m;
                int apiRequestId = 0;
                int planId = 0;
                TransactionDetail tx = null!;

                await using var dbTx = await _context.Database.BeginTransactionAsync(cancellationToken);
                try
                {
                    string appLockName = $"FZP_{user.Id}_{model.AccountNumber}_{model.Amount}";
                    int lockResult = (await _context.Database
                        .SqlQueryRaw<int>(
                            "DECLARE @r INT; EXEC @r = sp_getapplock @Resource = {0}, @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 5000; SELECT @r",
                            appLockName)
                        .ToListAsync()).First();

                    if (lockResult < 0)
                    {
                        await dbTx.RollbackAsync(cancellationToken);
                        return Fail("Transaction already in progress, please try again");
                    }

                    var duplicate = await _context.TransactionDetails.AnyAsync(
                        x => x.UserId == user.Id.ToString()
                          && x.Amount == model.Amount
                          && x.AccountNo == model.AccountNumber
                          && x.ServiceName == "DMT"
                          && x.ReqDate >= DateTime.Now.AddSeconds(-150),
                        cancellationToken);

                    if (duplicate || (model.Amount < 500 || model.Amount > 100000))
                    {
                        await dbTx.RollbackAsync(cancellationToken);
                        return Fail(duplicate ? "Duplicate Transaction" : "Amount should be in range of 500-100000");
                    }

                    decimal currentBalance = await _walletService.GetBalanceAsync(user.Id, cancellationToken);

                    planId = user.CommissionPlanId ?? 1;
                    rtComm = await GetCommissionFromPlanAsync(planId, Convert.ToDecimal(model.Amount), "RT");
                    totalDebit = Convert.ToDecimal(model.Amount) + rtComm;

                    if (currentBalance < totalDebit)
                    {
                        await dbTx.RollbackAsync(cancellationToken);
                        return Fail("Insufficient Balance");
                    }

                    newBal = currentBalance - totalDebit;
                    apiRequestId = new Random().Next(100000, 9999999);

                    tx = new TransactionDetail
                    {
                        UserId = Convert.ToString(user.Id),
                        UserName = user.Name + "-" + user.Phone,
                        WlId = user.Wlid,
                        MdId = user.Mdid,
                        AdId = user.Adid,
                        TxnId = apiRequestId.ToString(),
                        ServiceName = "DMT",
                        OperatorName = "Money Transfer",
                        OpId = null,
                        Mobileno = model.BeneficiaryMobile,
                        OldBal = currentBalance,
                        Amount = model.Amount,
                        Comm = 0,
                        Charge = rtComm,
                        Cost = totalDebit,
                        NewBal = Convert.ToString(newBal),
                        Status = "Pending",
                        Brid = null,
                        TxnType = "Debit",
                        ApiTxnId = apiRequestId.ToString(),
                        ApiName = "FZP",
                        AdminRemarks = null,
                        ApiMsg = null,
                        ApiRes = null,
                        ApiReq = Truncate(JsonConvert.SerializeObject(model), 4000),
                        ReqDate = DateTime.Now,
                        UpdateDate = DateTime.Now,
                        CustomerName = model.BeneficiaryName,
                        AccountNo = model.AccountNumber,
                        ComingFrom = model.ComingFrom,
                        IfscCode = model.IFSC,
                        BankName = model.BankName,
                        Tds = 0,
                        TxnMode = null,
                        MdComm = 0,
                        AdComm = 0,
                        WlComm = 0,
                        ServiceId = 6,
                        SuperAdminShare = 0
                    };

                    await _context.TransactionDetails.AddAsync(tx, cancellationToken);
                    await _context.SaveChangesAsync(cancellationToken);
                    await dbTx.CommitAsync(cancellationToken);
                }
                catch
                {
                    await dbTx.RollbackAsync(CancellationToken.None);
                    throw;
                }

                // Pre-debit: debit wallet before calling the API.
                // If debit fails for any reason, abort immediately — no API call is made.
                try
                {
                    var (_, _, debitEntryId) = await _walletService.DebitAsync(
                        user.Id, user.Name + "-" + user.Phone,
                        model.Amount ?? 0, totalDebit, rtComm, 0,
                        "Money_Transfer_Debit",
                        $"DMT Payment For Account No {tx.AccountNo}| Debit by Services | Amount Debit For DMT TxnId {tx.TxnId}",
                        user.Wlid, CancellationToken.None);

                    // Solid check: SELECT to confirm the debit row was persisted before calling the API.
                    bool debitVerified = debitEntryId > 0
                        && await _context.Tbluserbalances.AnyAsync(
                               b => b.Id == debitEntryId && b.UserId == user.Id, CancellationToken.None);

                    if (!debitVerified)
                    {
                        tx.Status = "FAILED";
                        tx.ApiMsg = "Wallet debit failed before API call";
                        tx.UpdateDate = DateTime.Now;
                        try { await _context.SaveChangesAsync(CancellationToken.None); } catch { }
                        return Fail("Please try again later, there is an issue with your wallet");
                    }
                }
                catch
                {
                    tx.Status = "FAILED";
                    tx.ApiMsg = "Wallet debit failed before API call";
                    tx.UpdateDate = DateTime.Now;
                    try { await _context.SaveChangesAsync(CancellationToken.None); } catch { }
                    return Fail("Please try again later, there is an issue with your wallet");
                }

                // Build Finzep payload
                var payload = new FinzepPayoutApiRequest
                {
                    UserID = _config.UserID,
                    Token = _config.Token,
                    OutletID = _config.OutletID,
                    PayoutRequest = new FinzepPayoutInner
                    {
                        AccountNo = model.AccountNumber,
                        AmountR = model.Amount ?? 0,
                        BankID =  10,
                        IFSC = model.IFSC,
                        SenderMobile = _config.SenderMobile,
                        SenderName = _config.SenderName,
                        SenderEmail = _config.SenderEmail,
                        BeneName = model.BeneficiaryName,
                        BeneMobile = _config.SenderMobile,
                        APIRequestID = apiRequestId,
                        SPKey = "IMPS",
                        WebHook = _config.WebhookUrl
                    }
                };

                var apiResponse = await CallFinzepPayoutApi(payload);

                string status = "PENDING";
                string operatorRef = "";
                string brid = "";

                if (apiResponse != null)
                {
                    status = MapStatus(apiResponse.status);
                    status = (apiResponse.status == 0 && apiResponse.statuscode == -1) ? "FAILED" : status;
                    operatorRef = apiResponse.rpid ?? apiRequestId.ToString();
                    brid = apiResponse.liveID ?? "";

                    tx.ApiTxnId = operatorRef;
                    tx.TxnId = operatorRef;
                    tx.ApiRes = JsonConvert.SerializeObject(apiResponse);
                    tx.ApiMsg = apiResponse.message;
                    tx.UpdateDate = DateTime.Now;
                    await _context.SaveChangesAsync(cancellationToken);

                    if (status == "SUCCESS")
                    {
                        tx.Status = "SUCCESS";
                        tx.Brid = brid;

                        await DistributeCommissionAsync(tx, user, Convert.ToDecimal(model.Amount), planId);
                        await _context.SaveChangesAsync(cancellationToken);

                        // Send transaction SMS
                        _ = _smsService.SendTransactionSmsAsync(model.BeneficiaryMobile, model.AccountNumber, model.Amount.ToString(), "65c4a796d6fc051e5652e402");
                    }
                    else if (status == "FAILED")
                    {
                        tx.Status = "FAILED";
                        tx.Brid = brid;
                        await _context.SaveChangesAsync(CancellationToken.None);
                        await RefundUserAsync(tx);
                    }
                    else
                    {
                        tx.Status = "PENDING";
                        tx.Brid = brid;
                        await _context.SaveChangesAsync(cancellationToken);
                        // Send transaction SMS
                        _ = _smsService.SendTransactionSmsAsync(model.BeneficiaryMobile, model.AccountNumber, model.Amount.ToString(), "65c4a796d6fc051e5652e402");
                    }
                }
                else
                {
                    status = "FAILED";
                    tx.Status = "FAILED";
                    tx.ApiRes = "API Error";
                    tx.UpdateDate = DateTime.Now;
                    await _context.SaveChangesAsync(CancellationToken.None);
                    await RefundUserAsync(tx);
                }

                var list = new List<DMTTXN>
                {
                    new DMTTXN
                    {
                        AccountNo = model.AccountNumber,
                        BeneName = model.BeneficiaryName,
                        Amount = model.Amount.ToString(),
                        Charge = rtComm.ToString("0.00"),
                        CurrentBalance = newBal.ToString("0.00"),
                        Status = status,
                        TxnID = apiRequestId.ToString(),
                        BR_Id = brid,
                        TxnDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")
                    }
                };

                return new LoginModel
                {
                    Status_Code = status == "SUCCESS" || status == "PENDING" ? "1" : "0",
                    Message = status == "SUCCESS" || status == "PENDING" ? "Transaction Successful" : "Transaction Failed || "+ apiResponse?.message ?? "",
                    Data = list
                };
            }
            catch (Exception ex)
            {
                return Fail("ERR:500 " + ex.Message);
            }
        }

        private async Task<FinzepPayoutApiResponse> CallFinzepPayoutApi(FinzepPayoutApiRequest payload)
        {
            var client = _httpClientFactory.CreateClient();
            var content = new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json");

            var res = await client.PostAsync(PayoutUrl, content);
            var json = await res.Content.ReadAsStringAsync();

            var log = new Apilog
            {
                Apiname = "Transaction-FZP",
                Reqdatae = DateTime.Now,
                Request = JsonConvert.SerializeObject(payload),
                Response = json
            };
            _context.Apilogs.Add(log);
            await _context.SaveChangesAsync();

            try
            {
                return JsonConvert.DeserializeObject<FinzepPayoutApiResponse>(json);
            }
            catch
            {
                return null;
            }
        }

        public async Task<LoginModel> CheckStatus(string txnId, CancellationToken cancellationToken)
        {
            try
            {
                var tx = await _context.TransactionDetails
                    .FirstOrDefaultAsync(x => x.TxnId == txnId || x.ApiTxnId == txnId, cancellationToken);

                if (tx == null)
                    return Fail("Transaction not found");

                if (string.IsNullOrEmpty(tx.ApiTxnId))
                    return Fail("ApiTxnId not found");

                if (tx.Status?.ToLower() != "pending")
                    return Fail("Transaction already processed");

                var client = _httpClientFactory.CreateClient();
                string optional1 = Uri.EscapeDataString(DateTime.Now.ToString("dd MMM yyyy"));
                string url = $"{StatusCheckUrl}?UserID={_config.UserID}&Token={_config.Token}&RPID={tx.TxnId}&AGENTID={_config.AgentID}&Optional1={optional1}&Format=1";

                var res = await client.GetAsync(url, cancellationToken);
                var json = await res.Content.ReadAsStringAsync();

                var log = new Apilog
                {
                    Apiname = "CheckStatus-FZP",
                    Reqdatae = DateTime.Now,
                    Request = tx.ApiTxnId,
                    Response = json
                };
                _context.Apilogs.Add(log);
                await _context.SaveChangesAsync(cancellationToken);

                FinzepStatusApiResponse data;
                try
                {
                    data = JsonConvert.DeserializeObject<FinzepStatusApiResponse>(json);
                }
                catch
                {
                    return Fail("Invalid response from Finzep");
                }

                if (data == null)
                    return Fail("Invalid response from Finzep");

                string status = MapStatus(data.status);

                if (status == "SUCCESS")
                {
                    tx.Status = "SUCCESS";
                    tx.ApiMsg = data.msg;
                    tx.UpdateDate = DateTime.Now;
                    tx.Brid = data.opid ?? "";
                    tx.ApiRes = json;
                    await _context.SaveChangesAsync(cancellationToken);

                    var userForComm = await _context.TblUsers.FirstOrDefaultAsync(x => x.Id == Convert.ToInt32(tx.UserId), cancellationToken);
                    if (userForComm != null)
                    {
                        await DistributeCommissionAsync(tx, userForComm, Convert.ToDecimal(tx.Amount), userForComm.CommissionPlanId ?? 1);
                    }

                    return SuccessResponse(tx, data.opid ?? "", "SUCCESS");
                }
                else if (status == "FAILED")
                {
                    tx.Status = "FAILED";
                    tx.ApiMsg = data.msg;
                    tx.Brid = data.opid ?? "";
                    tx.UpdateDate = DateTime.Now;
                    tx.ApiRes = json;
                    await _context.SaveChangesAsync(cancellationToken);
                    await RefundUserAsync(tx);
                    return SuccessResponse(tx, "", "FAILED");
                }
                else if (status == "REFUNDED")
                {
                    tx.Status = "REFUNDED";
                    tx.ApiMsg = data.msg;
                    tx.Brid = data.opid ?? "";
                    tx.UpdateDate = DateTime.Now;
                    tx.ApiRes = json;
                    await _context.SaveChangesAsync(cancellationToken);
                    await RefundUserAsync(tx);
                    return SuccessResponse(tx, "", "FAILED");
                }
                else
                {
                    tx.Status = "PENDING";
                    tx.ApiMsg = data.msg;
                    tx.Brid = data.opid ?? "";
                    tx.UpdateDate = DateTime.Now;
                    tx.ApiRes = json;
                    await _context.SaveChangesAsync(cancellationToken);
                    return SuccessResponse(tx, data.opid ?? "", "PENDING");
                }
            }
            catch (Exception ex)
            {
                return Fail("ERR:500 " + ex.Message);
            }
        }

        private LoginModel Fail(string msg)
        {
            return new LoginModel
            {
                Status_Code = "0",
                Message = msg,
                Data = null
            };
        }

        private LoginModel SuccessResponse(TransactionDetail tx, string operatorRef, string status)
        {
            var list = new List<DMTTXN>
            {
                new DMTTXN
                {
                    AccountNo = tx.AccountNo,
                    BeneName = tx.CustomerName,
                    Amount = tx.Amount.ToString(),
                    Charge = tx.Charge.ToString(),
                    CurrentBalance = tx.NewBal,
                    Status = status,
                    TxnID = tx.TxnId,
                    BR_Id = operatorRef,
                    TxnDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")
                }
            };

            return new LoginModel
            {
                Status_Code = status == "SUCCESS" ? "1" : "0",
                Message = status,
                Data = list
            };
        }
    }
}

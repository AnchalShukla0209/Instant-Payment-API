using InstantPay.Application.Interfaces;
using InstantPay.Application.Interfaces.MoneyTransfer.Castler;
using InstantPay.Infrastructure.Sql.Entities;
using InstantPay.SharedKernel.Entity.CastlerConfigDTO;
using InstantPay.SharedKernel.RequestPayload.MoneyTransfer.Castler;
using InstantPay.SharedKernel.Results.MoneyTransfer.Castler;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using static System.Net.WebRequestMethods;

namespace InstantPay.Application.Services.MoneyTransfer
{
    public class CastlerDmtService : ICastlerDmtService
    {
        private readonly CastlerConfig _config;
        private readonly ICastlerAuthService _auth;
        private readonly AppDbContext _context;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IWalletService _walletService;

        public CastlerDmtService(IOptions<CastlerConfig> config, ICastlerAuthService repo, AppDbContext context, IHttpClientFactory httpClientFactory, IWalletService walletService)
        {
            _httpClientFactory = httpClientFactory;
            _config = config.Value;
            _auth = repo;
            _context = context;
            _walletService = walletService;
        }

        private async Task<decimal> GetCommissionAsync(
        int planId, int slabId, decimal amount,
        string shareColumn)
        {
            if (slabId == 0) return 0m;

            var slab = await _context.Tblcommissionslabs
                .Where(x => x.PlanId == Convert.ToString(planId) && x.SlabId == Convert.ToString(slabId))
                .FirstOrDefaultAsync();

            if (slab == null) return 0m;

            decimal share = shareColumn switch
            {
                "RT" => (decimal)slab.Rtshare,
                "AD" => (decimal)slab.Adshare,
                "MD" => (decimal)slab.Mdshare,
                "WL" => (decimal)slab.WlShare,
                _ => 0m
            };

            decimal commission = slab.CommissionType == "RS"
                ? share
                : (amount * share / 100);

            return commission;
        }

        private static string Truncate(string input, int maxLen)
        {
            if (string.IsNullOrEmpty(input)) return string.Empty;
            return input.Length <= maxLen ? input : input.Substring(0, maxLen);
        }

        private static string RedactSensitive(string input)
        {
            if (string.IsNullOrEmpty(input)) return string.Empty;
            try
            {
                var redacted = System.Text.RegularExpressions.Regex.Replace(input, @"\d{12,}", "[REDACTED]");
                redacted = System.Text.RegularExpressions.Regex.Replace(redacted, @"[A-Za-z0-9+/]{40,}=*", "[REDACTED]");
                return redacted;
            }
            catch
            {
                return "[REDACTED]";
            }
        }

        public bool VerifyPin(string inputPin, string TxnPin)
        {
            if (inputPin.ToLower().Trim() == TxnPin.ToLower().Trim())
            {
                return true;
            }
            return false;
        }

        public async Task<LoginModel> MoneyTransfer(DmtRequest model, string ip, CancellationToken cancellationToken)
        {
            using var dbTransaction = await _context.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                var user = await _context.TblUsers.FirstOrDefaultAsync(x => x.Id == Convert.ToInt32(model.UserId), cancellationToken);
                if (user == null)
                    return Fail("User not found");

                if (user == null || !VerifyPin(model.TransactionPin, user.TxnPin))
                    return Fail("Invalid Pin");

                var duplicate = await _context.TransactionDetails.AnyAsync(x => x.UserId == user.Id.ToString() && x.Amount == model.Amount && x.AccountNo == model.AccountNumber && x.ServiceName == "Money Transfer" && x.ReqDate >= DateTime.Now.AddSeconds(-150), cancellationToken);
                if (duplicate)
                    return Fail("Duplicate Transaction");


                decimal currentBalance = await _walletService.GetBalanceAsync(user.Id, cancellationToken);

                decimal charge = (decimal)(model.Amount * 0.4m / 100);
                decimal totalDebit = (decimal)(model.Amount + charge);

                if (currentBalance < totalDebit)
                    return Fail("Insufficient Balance");

                int slabId = model.Amount switch
                {
                    0 => 0,
                    <= 1000 => 23,
                    <= 2000 => 24,
                    <= 3000 => 25,
                    <= 4000 => 26,
                    <= 5000 => 27,
                    <= 10000 => 28,
                    <= 15000 => 29,
                    <= 20000 => 30,
                    <= 25000 => 31,
                    <= 50000 => 37,
                    _ => 0
                };

                int mainPlanId = Convert.ToInt32(user.PlanId);
                decimal rtComm = await GetCommissionAsync(mainPlanId, slabId, Convert.ToDecimal(model.Amount), "RT");
                decimal adComm = await GetCommissionAsync(mainPlanId, slabId, Convert.ToDecimal(model.Amount), "AD");
                decimal mdComm = await GetCommissionAsync(mainPlanId, slabId, Convert.ToDecimal(model.Amount), "MD");
                decimal wlComm = await GetCommissionAsync(mainPlanId, slabId, Convert.ToDecimal(model.Amount), "WL");

                if (user.Usertype == "RT")
                {
                    if (user.Adid == "0" && user.Mdid == "0")
                    {
                        wlComm -= rtComm;
                        adComm = 0;
                        mdComm = 0;
                    }
                    else if (user.Adid == "0" && user.Mdid != "0")
                    {
                        mdComm -= rtComm;
                        wlComm -= mdComm;
                        adComm = 0;
                    }
                    else if (user.Adid != "0" && user.Mdid != "0")
                    {
                        adComm -= rtComm;
                        mdComm -= adComm;
                        wlComm -= mdComm;
                    }
                    else if (user.Adid != "0" && user.Mdid == "0")
                    {
                        adComm -= rtComm;
                        mdComm = 0;
                        wlComm -= adComm;
                    }
                }

                decimal tds = rtComm * 5 / 100;
                decimal cost = (Convert.ToDecimal(model.Amount) + rtComm) - tds;
                decimal Newbal = currentBalance - cost;

                string requestBody = JsonConvert.SerializeObject(model);

                string refId = DateTime.UtcNow.Ticks.ToString().Substring(0, 15);

                var tx = new TransactionDetail
                {
                    UserId = Convert.ToString(user.Id),
                    UserName = user?.Name + "-" + user?.Phone,
                    WlId = user?.Wlid,
                    MdId = user?.Mdid,
                    AdId = user?.Adid,
                    TxnId = refId,
                    ServiceName = "DMT",
                    OperatorName = "Money Transfer",
                    OpId = null,
                    Mobileno = model.Mobile,
                    OldBal = currentBalance,
                    Amount = model.Amount,
                    Comm = 0,
                    Charge = rtComm,
                    Cost = cost,
                    NewBal = Convert.ToString(Newbal),
                    Status = "Pending",
                    Brid = null,
                    TxnType = "Debit",
                    ApiTxnId = refId,
                    ApiName = "Castler",
                    AdminRemarks = null,
                    ApiMsg = null,
                    ApiRes = null,
                    ApiReq = Truncate(RedactSensitive(requestBody), 4000),
                    ReqDate = DateTime.Now,
                    UpdateDate = DateTime.Now,
                    CustomerName = model.Mobile,
                    AccountNo = model.AccountNumber,
                    ComingFrom = "Web",
                    IfscCode = model.IFSC,
                    BankName = model.BankName,
                    Tds = rtComm * 5 / 100,
                    TxnMode = null,
                    MdComm = mdComm,
                    AdComm = adComm,
                    WlComm = wlComm,
                    ServiceId = 6
                };

                await _context.TransactionDetails.AddAsync(tx, cancellationToken);

                decimal actualNewbal;
                (_, actualNewbal, _) = await _walletService.DebitAsync(
                    user.Id, user.Name + "-" + user.Phone,
                    model.Amount ?? 0, cost, rtComm, tds,
                    "Money_Transfer_Debit",
                    $"Money Transfer CASH DEPOSIT TXN:{tx.TxnId}",
                    user.Wlid, cancellationToken);
                Newbal = actualNewbal;

                string token = await _auth.GetToken();
                if (string.IsNullOrEmpty(token))
                    token = await _auth.GenerateToken();

                // PAYEE
                if (string.IsNullOrEmpty(model.PayeeId))
                    model.PayeeId = await AddPayee(model, token);

                // API CALL
                var apiResponse = await CallCastlerApi(model, refId, token, ip);

                string status = "FAILED";
                string operatorRef = "";

                if (apiResponse != null && apiResponse.Success)
                {
                    if (apiResponse.Result.Status == "Success" || apiResponse.Result.Status == "Pending for Authorization")
                        status = "SUCCESS";
                    else if (apiResponse.Result.Status.Contains("Pending"))
                        status = "PENDING";
                    else
                        status = "FAILED";

                    operatorRef = apiResponse.Result.TransferId;

                    tx.Status = status;
                    tx.Brid = operatorRef;
                    tx.ApiRes = JsonConvert.SerializeObject(apiResponse.Result);

                    await _context.SaveChangesAsync(cancellationToken);
                    await dbTransaction.CommitAsync(cancellationToken);
                }
                else
                {
                    // REFUND — atomically credit back the debited amount
                    await _walletService.CreditAsync(
                        user.Id, user.Name + "-" + user.Phone,
                        model.Amount ?? 0, cost, rtComm, tds,
                        "Money_Transfer_Refund",
                        $"Refund For Money Transfer CASH DEPOSIT TXN:{tx.TxnId}",
                        user.Wlid, cancellationToken);


                    tx.Status = "FAILED";

                    await _context.SaveChangesAsync(cancellationToken);
                    await dbTransaction.CommitAsync(cancellationToken);
                }

                string finalMessage = "Transaction Failed";

                if (apiResponse != null && apiResponse.Success)
                {
                    if (apiResponse.Result.Status == "Pending for Authorization")
                        finalMessage = "Success";
                    else
                        finalMessage = apiResponse.Result.Status;
                }

                var list = new List<DMTTXN>
                {
                    new DMTTXN
                    {
                        AccountNo = model.AccountNumber,
                        BeneName = model.BeneficiaryName,
                        Amount = model.Amount.ToString(),
                        Charge = rtComm.ToString("0.00"),
                        CurrentBalance = Newbal.ToString("0.00"),
                        Status = status,
                        TxnID = refId,
                        BR_Id = operatorRef,
                        TxnDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")
                    }
                };

                return new LoginModel
                {
                    Status_Code = status == "SUCCESS" ? "1" : "0",
                    Message = finalMessage,
                    Data = list
                };




            }
            catch (Exception ex)
            {
                await dbTransaction.RollbackAsync(cancellationToken);
                return Fail("ERR:500 " + ex.Message);
            }
        }

        private async Task<CastlerTransferResponse> CallCastlerApi(DmtRequest model, string refId, string token, string ip)
        {
            var client = _httpClientFactory.CreateClient();

            client.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");
            client.DefaultRequestHeaders.Add("x-api-key", _config.XApiKey);

            var body = new
            {
                payeeId = model.PayeeId,
                bankAccountNumber = _config.ApiAccNo,
                amount = model.Amount,
                customerRefId = refId,
                purpose = "settlement",
                transferType = model.TransferMode == 2 ? "IMPS" : "NEFT",
                notes = new { userId = model.UserId, custIpAddress = ip }
            };

            var res = await client.PostAsync(
                $"{_config.BaseUrl}api/v1/bank-account/transfer",
                new StringContent(JsonConvert.SerializeObject(body), Encoding.UTF8, "application/json")
            );

            var json = await res.Content.ReadAsStringAsync();
            var logdetail = new Apilog
            {
                Apiname = "Transaction-Castler",
                Reqdatae = DateTime.Now,
                Request = JsonConvert.SerializeObject(body),
                Response = json
            };
            _context.Apilogs.Add(logdetail);
            await _context.SaveChangesAsync();

            return JsonConvert.DeserializeObject<CastlerTransferResponse>(json);
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

        private async Task<string> AddPayee(DmtRequest model, string token)
        {
            var client = _httpClientFactory.CreateClient();

            client.DefaultRequestHeaders.Clear();
            client.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");
            client.DefaultRequestHeaders.Add("x-api-key", _config.XApiKey);

            var body = new
            {
                accountHolder = model.BeneficiaryName,
                accountNumber = model.AccountNumber,
                bankName = model.BankName,
                bankAddress = "NA",
                ifsc = model.IFSC,
                mobile = model.Mobile,
                email = "test@gmail.com"
            };

            var res = await client.PostAsync(
                $"{_config.BaseUrl}api/v1/payee",
                new StringContent(JsonConvert.SerializeObject(body), Encoding.UTF8, "application/json")
            );

            var json = await res.Content.ReadAsStringAsync();

            var logdetail = new Apilog
            {
                Apiname = "AddPayee-Castler",
                Reqdatae = DateTime.Now,
                Request = JsonConvert.SerializeObject(body),
                Response = json
            };
            _context.Apilogs.Add(logdetail);
            await _context.SaveChangesAsync();

            var data = JsonConvert.DeserializeObject<PayeeResponse>(json);

            if (data != null && data.Success && data.Result != null)
                return data.Result.PayeeId;

            if (data?.Errors != null && data.Errors.Count > 0 &&
                data.Errors[0].Contains("already registered"))
            {
                var listRes = await client.GetAsync(
                    $"{_config.BaseUrl}api/v1/payee?s={model.AccountNumber}"
                );

                var listJson = await listRes.Content.ReadAsStringAsync();

                var listData = JsonConvert.DeserializeObject<PayeeListResponse>(listJson);

                if (listData != null && listData.Success && listData.Result.Count > 0)
                {
                    return listData.Result[0].PayeeId;
                }
            }

            return null;
        }

        public async Task<LoginModel> CheckStatus(string txnId, CancellationToken cancellationToken)
        {
            try
            {
               
                var tx = await _context.TransactionDetails
                    .FirstOrDefaultAsync(x => x.Brid == txnId, cancellationToken);

                if (tx == null)
                    return Fail("Transaction not found");

                //if (tx.Status == "SUCCESS")
                //    return Fail("Transaction already updated");

                if (string.IsNullOrEmpty(tx.ApiTxnId))
                    return Fail("TransferId not found");

                var token = await _auth.GetToken();
                var client = _httpClientFactory.CreateClient();
                client.DefaultRequestHeaders.Clear();
                client.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");
                client.DefaultRequestHeaders.Add("x-api-key", _config.XApiKey);

                // ✅ 3. CALL STATUS API
                var res = await client.GetAsync(
                    $"{_config.BaseUrl}api/v1/bank-account/transfer/{txnId}"
                );

                var json = await res.Content.ReadAsStringAsync();

                var log = new Apilog
                {
                    Apiname = "CheckStatus-Castler",
                    Reqdatae = DateTime.Now,
                    Request = tx.ApiTxnId,
                    Response = json
                };
                _context.Apilogs.Add(log);

                var data = JsonConvert.DeserializeObject<CastlerTransferResponse>(json);

                if (data != null && data.Success)
                {
                    string apiStatus = data.Result.Status.ToLower().Trim();

                    if (apiStatus == "success")
                    {
                        tx.Status = "SUCCESS";
                        tx.ApiMsg = apiStatus;
                        tx.UpdateDate = DateTime.Now;
                        tx.Brid = data?.Result?.utr ?? ""; 
                        tx.ApiRes = json;

                        await _context.SaveChangesAsync(cancellationToken);
                        await HandleCallback(tx, data);
                        return SuccessResponse(tx, data.Result.TransferId, "SUCCESS");
                    }
                    else
                    {
                        tx.ApiMsg = apiStatus;
                        tx.UpdateDate = DateTime.Now;
                        tx.ApiRes = json;
                        await _context.SaveChangesAsync(cancellationToken);
                        return SuccessResponse(tx, data.Result.TransferId, apiStatus);
                    }
                }
                else
                {
                    return Fail(data?.Errors != null && data?.Errors.Count()>0 ? data.Errors[0] : "Status check failed");
                }
            }
            catch (Exception ex)
            {
                return Fail("ERR:500 " + ex.Message);
            }
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

        private async Task HandleCallback(TransactionDetail tx, CastlerTransferResponse data)
        {
            tx.Status = "SUCCESS";
            tx.ApiMsg = data.Result.Status;
            tx.UpdateDate = DateTime.Now;
            await _context.SaveChangesAsync();
        }


    }
}

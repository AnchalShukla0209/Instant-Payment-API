using InstantPay.Application.Interfaces;
using InstantPay.Application.Interfaces.MoneyTransfer.NIFI;
using InstantPay.Infrastructure.Security;
using InstantPay.Infrastructure.Sql.Entities;
using InstantPay.SharedKernel.Entity.NIFIConfigDTO;
using InstantPay.SharedKernel.RequestPayload.MoneyTransfer.NIFI;
using InstantPay.SharedKernel.Results;
using InstantPay.SharedKernel.Results.MoneyTransfer.Castler;
using InstantPay.SharedKernel.Results.MoneyTransfer.NIFI;
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
    public class NifiDmtService : INifiDmtService
    {
        private readonly NIFIConfig _config;
        private readonly AppDbContext _context;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IWalletService _walletService;
        private readonly ICommissionService _commissionService;

        private const int ServiceId = 6;
        private const string ApiCode = "NIFI";

        public NifiDmtService(IOptions<NIFIConfig> config, AppDbContext context, IHttpClientFactory httpClientFactory, IWalletService walletService, ICommissionService commissionService)
        {
            _httpClientFactory = httpClientFactory;
            _config = config.Value;
            _context = context;
            _walletService = walletService;
            _commissionService = commissionService;
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

        private Task<decimal> GetCommissionFromPlanAsync(
            int planId, decimal amount, string shareColumn)
            => _commissionService.GetCommissionFromPlanAsync(planId, amount, ServiceId, ApiCode, shareColumn);

        private Task DistributeCommissionAsync(
            TransactionDetail tx, TblUser user, decimal amount, int planId)
            => _commissionService.DistributeCommissionAsync(
                tx, user, amount, planId, ServiceId, ApiCode,
                "NIFI Commission Credit");

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

        public async Task<LoginModel> MoneyTransfer(NifiDmtRequest model, string ip, CancellationToken cancellationToken)
        {
            try
            {
                var user = await _context.TblUsers.FirstOrDefaultAsync(x => x.Id == Convert.ToInt32(model.UserId), cancellationToken);
                if (user == null)
                    return Fail("User not found");

                if (user == null || !VerifyPin(model.TransactionPin, user.TxnPin))
                    return Fail("Invalid Pin");

                decimal rtComm = 0m;
                decimal totalDebit = 0m;
                decimal Newbal = 0m;
                string refId = string.Empty;
                int planId = 0;
                TransactionDetail tx = null!;

                await using var dbTx = await _context.Database.BeginTransactionAsync(cancellationToken);
                try
                {
                    string appLockName = $"NIFI_{user.Id}_{model.AccountNumber}_{model.Amount}";
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

                    var duplicate = await _context.TransactionDetails.AnyAsync(x => x.UserId == user.Id.ToString() && x.Amount == model.Amount && x.AccountNo == model.AccountNumber && x.ServiceName == "Money Transfer" && x.ReqDate >= DateTime.Now.AddSeconds(-150), cancellationToken);
                    if (duplicate)
                    {
                        await dbTx.RollbackAsync(cancellationToken);
                        return Fail("Duplicate Transaction");
                    }

                    decimal currentBalance = await _walletService.GetBalanceAsync(user.Id, cancellationToken);

                    // Get commission for RT
                    planId = user.CommissionPlanId ?? 1;
                    rtComm = await GetCommissionFromPlanAsync(planId, Convert.ToDecimal(model.Amount), "RT");
                    totalDebit = Convert.ToDecimal(model.Amount) + rtComm;

                    if (currentBalance < totalDebit)
                    {
                        await dbTx.RollbackAsync(cancellationToken);
                        return Fail("Insufficient Balance");
                    }

                    Newbal = currentBalance - totalDebit;

                    refId = "NIFI" + DateTime.UtcNow.Ticks.ToString().Substring(0, 10);

                    tx = new TransactionDetail
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
                        Cost = totalDebit,
                        NewBal = Convert.ToString(Newbal),
                        Status = "Pending",
                        Brid = null,
                        TxnType = "Debit",
                        ApiTxnId = refId,
                        ApiName = "NIFI",
                        AdminRemarks = null,
                        ApiMsg = null,
                        ApiRes = null,
                        ApiReq = Truncate(RedactSensitive(JsonConvert.SerializeObject(model)), 4000),
                        ReqDate = DateTime.Now,
                        UpdateDate = DateTime.Now,
                        CustomerName = model.BeneficiaryName,
                        AccountNo = model.AccountNumber,
                        ComingFrom = "Web",
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
                decimal actualNewBal = Newbal;
                try
                {
                    int debitEntryId;
                    (_, actualNewBal, debitEntryId) = await _walletService.DebitAsync(
                        user.Id, user.Name + "-" + user.Phone,
                        model.Amount ?? 0, totalDebit, rtComm, 0,
                        "Money_Transfer_Debit",
                        $"Money Transfer CASH DEPOSIT TXN:{tx.TxnId}",
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
                        return Fail("Debit entry not found in tbluserbalance after insert");
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

                // Prepare NIFI payload
                var nifiPayload = new NifiPayoutRequest
                {
                    p1 = model.AccountNumber,
                    p2 = model.IFSC,
                    p3 = refId,
                    p4 = model.Amount.ToString(),
                    p5 = model.BeneficiaryName,
                    p6 = model.Mobile,
                    p7 = user?.EmailId ?? "krishany365@gmail.com",
                    p8 = user?.Name ?? "Krishan",
                    p9 = "Payout",
                    p10 = "1",
                    p11 = $"{user?.Lat ?? "28.623957"},{user?.Longitute ?? "77.352528"}",
                    p72 = "1",
                    p73 = "9813090728",
                    p74 = "false"
                };

                string payloadJson = JsonConvert.SerializeObject(nifiPayload);
                string encryptedPayload = NifiEncryptionService.Encrypt(payloadJson, _config.EncryptionKey, _config.EncryptionIv);

                var encryptedRequest = new NifiEncryptedRequest
                {
                    body = encryptedPayload
                };

                // API CALL
                var apiResponse = await CallNifiApi(encryptedRequest);

                string status = "PENDING";
                string operatorRef = "";

                if (apiResponse != null)
                {
                    string apiStatus = apiResponse.status?.ToLower().Trim() ?? "";
                    operatorRef = apiResponse.data?.bank_ref_num ?? "";

                    // SUCCESS: statuscode=10000, status=success, and bank_ref_num exists
                    if (apiResponse.statuscode == 10000 && apiStatus == "success" && !string.IsNullOrEmpty(operatorRef))
                    {
                        status = "SUCCESS";
                        tx.Status = status;
                        tx.Brid = operatorRef;
                        tx.TxnId = apiResponse?.data?.externalRef ?? "";
                        tx.ApiRes = JsonConvert.SerializeObject(apiResponse);
                        tx.UpdateDate = DateTime.Now;
                        tx.NewBal = Convert.ToString(actualNewBal);

                        // Distribute commission on SUCCESS
                        await DistributeCommissionAsync(tx, user, Convert.ToDecimal(model.Amount), planId);

                        // Save TransactionDetail with updated commission values
                        await _context.SaveChangesAsync(cancellationToken);
                    }
                    // FAILED: status is error or failed
                    else if (apiStatus == "failed")
                    {
                        status = "FAILED";
                        tx.Status = status;
                        tx.TxnId = apiResponse?.data?.externalRef ?? "";
                        tx.ApiRes = JsonConvert.SerializeObject(apiResponse);
                        tx.UpdateDate = DateTime.Now;
                        await _context.SaveChangesAsync(CancellationToken.None);
                        await RefundUserAsync(tx);
                    }
                    // PENDING: status not error, not failed, not success
                    else
                    {
                        status = "PENDING";
                        tx.Status = status;
                        tx.Brid = operatorRef;
                        tx.TxnId = apiResponse?.data?.externalRef ?? "";
                        tx.ApiRes = JsonConvert.SerializeObject(apiResponse);
                        tx.UpdateDate = DateTime.Now;
                        await _context.SaveChangesAsync(cancellationToken);
                    }
                }
                else
                {
                    // API Error - treat as FAILED
                    status = "FAILED";
                    tx.Status = status;
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
                        CurrentBalance = Newbal.ToString("0.00"),
                        Status = status,
                        TxnID = refId,
                        BR_Id = operatorRef,
                        TxnDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")
                    }
                };

                return new LoginModel
                {
                    Status_Code = status.ToUpper().Trim() == "SUCCESS" || status.ToUpper().Trim() == "PENDING" ? "1" : "0",
                    Message = status.ToUpper().Trim() == "SUCCESS" || status.ToUpper().Trim() == "PENDING" ? "Transaction Successful" : "Transaction Failed",
                    Data = list
                };
            }
            catch (Exception ex)
            {
                return Fail("ERR:500 " + ex.Message);
            }
        }

        private async Task<NifiPayoutResponse> CallNifiApi(NifiEncryptedRequest request)
        {
            var client = _httpClientFactory.CreateClient();

            client.DefaultRequestHeaders.Add("x-client-id", _config.ClientId);
            client.DefaultRequestHeaders.Add("x-api-key", _config.ApiKey);

            var res = await client.PostAsync(
                "https://api.nifipayment.in/api/fi/initiate-single-payout/imps",
                new StringContent(JsonConvert.SerializeObject(request), Encoding.UTF8, "application/json")
            );

            var json = await res.Content.ReadAsStringAsync();

            var logdetail = new Apilog
            {
                Apiname = "Transaction-NIFI",
                Reqdatae = DateTime.Now,
                Request = JsonConvert.SerializeObject(request),
                Response = json
            };
            _context.Apilogs.Add(logdetail);
            await _context.SaveChangesAsync();

            var encryptedResponse = JsonConvert.DeserializeObject<NifiEncryptedResponse>(json);

            if (encryptedResponse != null && !string.IsNullOrEmpty(encryptedResponse.body))
            {
                try
                {
                    string decryptedJson = NifiEncryptionService.Decrypt(encryptedResponse.body, _config.EncryptionKey, _config.EncryptionIv);
                    return JsonConvert.DeserializeObject<NifiPayoutResponse>(decryptedJson);
                }
                catch
                {
                    return null;
                }
            }

            return null;
        }

        public async Task<LoginModel> CheckStatus(string txnId, CancellationToken cancellationToken)
        {
            try
            {
                var tx = await _context.TransactionDetails
                    .FirstOrDefaultAsync(x => x.TxnId == txnId || x.ApiTxnId == txnId, cancellationToken);

                if (tx == null)
                {
                    return Fail("Transaction not found");
                }

                if (string.IsNullOrEmpty(tx.ApiTxnId))
                {
                    return Fail("TransferId not found");
                }

                if (tx.Status?.ToLower() != "pending")
                {
                    return Fail("Transaction already processed");
                }

                var client = _httpClientFactory.CreateClient();
                client.DefaultRequestHeaders.Clear();
                client.DefaultRequestHeaders.Add("x-client-id", _config.ClientId);
                client.DefaultRequestHeaders.Add("x-api-key", _config.ApiKey);

                // Encrypt the transaction ID
                string encryptedTxnId = NifiEncryptionService.Encrypt(tx.ApiTxnId, _config.EncryptionKey, _config.EncryptionIv);

                var res = await client.GetAsync(
                    $"https://api.nifipayment.in/api/get-txn-status/{encryptedTxnId}"
                );

                var json = await res.Content.ReadAsStringAsync();

                var log = new Apilog
                {
                    Apiname = "CheckStatus-NIFI",
                    Reqdatae = DateTime.Now,
                    Request = tx.ApiTxnId,
                    Response = json
                };
                _context.Apilogs.Add(log);

                var encryptedResponse = JsonConvert.DeserializeObject<NifiEncryptedResponse>(json);

                if (encryptedResponse != null && !string.IsNullOrEmpty(encryptedResponse.body))
                {
                    try
                    {
                        string decryptedJson = NifiEncryptionService.Decrypt(encryptedResponse.body, _config.EncryptionKey, _config.EncryptionIv);
                        var data = JsonConvert.DeserializeObject<NifiStatusResponse>(decryptedJson);
                        string apiStatus = data.status?.ToLower().Trim() ?? "";
                        string operatorRef = data?.data.BankRefNo ?? "";
                        if (data != null && data.statuscode == 10000)
                        {
                            if (apiStatus == "success" && !string.IsNullOrEmpty(operatorRef))
                            {
                                tx.Status = "SUCCESS";
                                tx.ApiMsg = apiStatus;
                                tx.UpdateDate = DateTime.Now;
                                tx.Brid = data?.data?.BankRefNo ?? "";
                                tx.ApiRes = decryptedJson;
                                await _context.SaveChangesAsync(cancellationToken);
                                return SuccessResponse(tx, data.data.BankRefNo, "SUCCESS");
                            }
                            else if (apiStatus == "failed")
                            {
                                tx.Status = "FAILED";
                                tx.ApiMsg = apiStatus;
                                tx.UpdateDate = DateTime.Now;
                                tx.ApiRes = decryptedJson;
                                await _context.SaveChangesAsync();
                                var user = await _context.TblUsers.FirstOrDefaultAsync(x => x.Id == Convert.ToInt32(tx.UserId));
                                decimal rtComm = Convert.ToDecimal(tx.Charge);
                                decimal totalDebit = Convert.ToDecimal(tx.Amount) + rtComm;
                                await _walletService.CreditAsync(
                                    user.Id, user.Username + "-" + user.Phone,
                                    tx.Amount ?? 0, totalDebit, rtComm, 0,
                                    "Money_Transfer_Refund",
                                    $"Money Transfer Refunded TXN:{tx.TxnId}",
                                    user.Wlid);
                            }
                            else
                            {
                                tx.Status = "PENDING";
                                tx.ApiMsg = apiStatus;
                                tx.UpdateDate = DateTime.Now;
                                tx.ApiRes = decryptedJson;
                                await _context.SaveChangesAsync(cancellationToken);
                                return SuccessResponse(tx, data.data.BankRefNo, apiStatus);
                            }
                        }
                        else
                        {
                            return Fail(data?.message ?? "Status check failed");
                        }
                    }
                    catch
                    {
                        try
                        {
                            string decryptedJson = NifiEncryptionService.Decrypt(encryptedResponse.body, _config.EncryptionKey, _config.EncryptionIv);
                            var errorResponse = JsonConvert.DeserializeObject<NifiErrorResponse>(decryptedJson);
                            return Fail(errorResponse?.message ?? "Decryption failed");
                        }
                        catch
                        {
                            return Fail("Decryption failed");
                        }
                    }
                }

                return Fail("Invalid response");
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

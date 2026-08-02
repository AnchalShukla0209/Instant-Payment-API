using InstantPay.Application.Interfaces;
using InstantPay.Application.Interfaces.MoneyTransfer.AeronPay;
using InstantPay.Application.Interfaces.SMS;
using InstantPay.Infrastructure.Sql.Entities;
using InstantPay.SharedKernel.Entity.AeronpayConfigDTO;
using InstantPay.SharedKernel.RequestPayload.MoneyTransfer.AeronPay;
using InstantPay.SharedKernel.Results.MoneyTransfer.AeronPay;
using InstantPay.SharedKernel.Results.MoneyTransfer.Castler;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace InstantPay.Application.Services.MoneyTransfer
{
    public class AeronpayDmtService : IAeronpayDmtService
    {
        private readonly AeronpayConfig _config;
        private readonly AppDbContext _context;
        private readonly IWalletService _walletService;
        private readonly ICommissionService _commissionService;
        private readonly ISmsService _smsService;

        private const int ServiceId = 6;
        private const string ApiCode = "ARP";

        public AeronpayDmtService(IOptions<AeronpayConfig> config, AppDbContext context, IWalletService walletService, ICommissionService commissionService, ISmsService smsService)
        {
            _config = config.Value;
            _context = context;
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

        private static string MapStatus(string aeronpayStatus)
        {
            return aeronpayStatus?.ToUpper() switch
            {
                "SUCCESS" => "SUCCESS",
                "PENDING" => "PENDING",
                "FAILED" => "FAILED",
                _ => "FAILED"
            };
        }

        private Task<decimal> GetCommissionFromPlanAsync(int planId, decimal amount, string shareColumn)
            => _commissionService.GetCommissionFromPlanAsync(planId, amount, ServiceId, ApiCode, shareColumn);

        private Task DistributeCommissionAsync(TransactionDetail tx, TblUser user, decimal amount, int planId)
            => _commissionService.DistributeCommissionAsync(
                tx, user, amount, planId, ServiceId, ApiCode,
                $"Commission Credit DMT Payment For Account No {tx.AccountNo}| Credit by Services");

        private async Task RefundUserAsync(TransactionDetail tx)
        {
            var user = await _context.TblUsers.FirstOrDefaultAsync(x => x.Id == Convert.ToInt32(tx.UserId));
            if (user == null) return;

            decimal rtComm = Convert.ToDecimal(tx.Charge);
            decimal totalRefund = Convert.ToDecimal(tx.Amount) + rtComm;

            var (_, _, creditEntryId) = await _walletService.CreditAsync(
                user.Id, user.Username + "-" + user.Phone,
                tx.Amount ?? 0, totalRefund, rtComm, 0,
                "Money_Transfer_Refund",
                $"Money Transfer Refunded | DMT Payment For Account No {tx.AccountNo}| Credit by Services | Refund Credit TXN:{tx.TxnId}",
                user.Wlid);

            bool creditVerified = creditEntryId > 0
                && await _context.Tbluserbalances.AnyAsync(
                       b => b.Id == creditEntryId && b.UserId == user.Id);

            if (!creditVerified)
            {
                tx.Status = "PENDING";
                tx.ApiMsg = "Refund credit not verified — kept as PENDING for retry";
                tx.UpdateDate = DateTime.Now;
                try { await _context.SaveChangesAsync(); } catch { }
                return;
            }
        }

        public async Task<LoginModel> MoneyTransfer(AeronpayDmtRequest model, string ip, CancellationToken cancellationToken)
        {
            try
            {
                var user = await _context.TblUsers.FirstOrDefaultAsync(x => x.Id == Convert.ToInt32(model.UserId), cancellationToken);
                if (user == null)
                    return Fail("User not found");

                if (!VerifyPin(model.TransactionPin, user.TxnPin))
                    return Fail("Invalid Pin");

                decimal rtComm = 0m;
                decimal totalDebit = 0m;
                decimal newBal = 0m;
                string clientReferenceId = string.Empty;
                int planId = 0;
                TransactionDetail tx = null!;

                await using var dbTx = await _context.Database.BeginTransactionAsync(cancellationToken);
                try
                {
                    string appLockName = $"ARP_{user.Id}_{model.AccountNumber}_{model.Amount}";
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

                    if (duplicate || (model.Amount < 100 || model.Amount > 100000))
                    {
                        await dbTx.RollbackAsync(cancellationToken);
                        return Fail(duplicate ? "Duplicate Transaction" : "Amount should be in range of 100-100000");
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
                    int apiRequestId = new Random().Next(100000, 9999999);
                    clientReferenceId = "DMT" + DateTime.Now.ToString("yyyyMMddHHmmss") + apiRequestId.ToString();

                    tx = new TransactionDetail
                    {
                        UserId = Convert.ToString(user.Id),
                        UserName = user.Name + "-" + user.Phone,
                        WlId = user.Wlid,
                        MdId = user.Mdid,
                        AdId = user.Adid,
                        TxnId = clientReferenceId,
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
                        ApiTxnId = clientReferenceId,
                        ApiName = "ARP",
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

                var apiResponse = await CallAeronpayPayoutApi(model, user, clientReferenceId, cancellationToken);

                string status = "FAILED";
                string aeronTxnId = "";
                string utr = "";

                if (apiResponse != null)
                {
                    status = MapStatus(apiResponse.status);
                    aeronTxnId = apiResponse.data?.transactionId ?? "";
                    utr = apiResponse.data?.utr ?? "";

                    if (!string.IsNullOrEmpty(aeronTxnId))
                        tx.ApiTxnId = aeronTxnId;

                    tx.ApiRes = JsonConvert.SerializeObject(apiResponse);
                    tx.ApiMsg = apiResponse.message;
                    tx.Brid = utr;
                    tx.UpdateDate = DateTime.Now;
                    await _context.SaveChangesAsync(cancellationToken);

                    if (status == "SUCCESS")
                    {
                        tx.Status = "SUCCESS";

                        await DistributeCommissionAsync(tx, user, Convert.ToDecimal(model.Amount), planId);
                        await _context.SaveChangesAsync(cancellationToken);

                        _ = _smsService.SendTransactionSmsAsync(model.BeneficiaryMobile, model.AccountNumber, model.Amount.ToString(), "65c4a796d6fc051e5652e402");
                    }
                    else if (status == "PENDING")
                    {
                        tx.Status = "PENDING";
                        await _context.SaveChangesAsync(cancellationToken);

                        _ = _smsService.SendTransactionSmsAsync(model.BeneficiaryMobile, model.AccountNumber, model.Amount.ToString(), "65c4a796d6fc051e5652e402");
                    }
                    else
                    {
                        tx.Status = "FAILED";
                        await _context.SaveChangesAsync(CancellationToken.None);
                        await RefundUserAsync(tx);
                    }
                }
                else
                {
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
                        TxnID = clientReferenceId,
                        BR_Id = utr,
                        TxnDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")
                    }
                };

                return new LoginModel
                {
                    Status_Code = status == "SUCCESS" || status == "PENDING" ? "1" : "0",
                    Message = status == "SUCCESS" || status == "PENDING" ? "Transaction Successful" : "Transaction Failed || " + apiResponse?.message ?? "",
                    Data = list
                };
            }
            catch (Exception ex)
            {
                return Fail("ERR:500 " + ex.Message);
            }
        }

        private async Task<AeronpayPayoutApiResponse> CallAeronpayPayoutApi(AeronpayDmtRequest model, TblUser user, string clientReferenceId, CancellationToken cancellationToken)
        {
            try
            {
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
                ServicePointManager.Expect100Continue = false;

                var ipv4 = Dns.GetHostAddresses(_config.OriginalHost)
                              .FirstOrDefault(ip => ip.AddressFamily == AddressFamily.InterNetwork)?.ToString();

                if (string.IsNullOrEmpty(ipv4))
                    return null;

                string url = $"https://{ipv4}{_config.PayoutPath}";

                var bodyObj = new
                {
                    bankProfileId = "1",
                    transferMode = "IMPS",
                    remarks = "IMPS",
                    latitude = user.Lat ?? "28.6995277",
                    longitude = user.Longitute ?? "76.9250906",
                    accountNumber = _config.SenderAccountNumber,
                    amount = model.Amount?.ToString("F2"),
                    client_referenceId = clientReferenceId,
                    beneDetails = new
                    {
                        bankAccount = model.AccountNumber,
                        ifsc = model.IFSC,
                        name = model.BeneficiaryName,
                        email = user.EmailId ?? "",
                        phone = model.BeneficiaryMobile ?? "",
                        address1 = user.AddressLine1 ?? ""
                    }
                };

                string json = JsonConvert.SerializeObject(bodyObj);
                byte[] jsonBytes = Encoding.UTF8.GetBytes(json);

                var handler = new HttpClientHandler
                {
                    UseProxy = false,
                    AutomaticDecompression = DecompressionMethods.None,
                    ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true
                };

                using var client = new HttpClient(handler);
                var request = new HttpRequestMessage(HttpMethod.Post, url);
                request.Version = HttpVersion.Version11;
                request.Headers.Host = _config.OriginalHost;
                request.Headers.Add("client-id", _config.ClientId);
                request.Headers.Add("client-secret", _config.ClientSecret);
                request.Headers.Add("accept", "application/json");
                request.Headers.Add("User-Agent", "curl/7.81.0");
                request.Headers.ConnectionClose = true;

                request.Content = new ByteArrayContent(jsonBytes);
                request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
                request.Content.Headers.ContentLength = jsonBytes.Length;

                var response = await client.SendAsync(request, cancellationToken);
                string resp = await response.Content.ReadAsStringAsync(cancellationToken);

                var log = new Apilog
                {
                    Apiname = "Transaction-ARP",
                    Reqdatae = DateTime.Now,
                    Request = json,
                    Response = resp
                };
                _context.Apilogs.Add(log);
                await _context.SaveChangesAsync(cancellationToken);

                return JsonConvert.DeserializeObject<AeronpayPayoutApiResponse>(resp);
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
                {
                    // ── Settlement Withdrawal fallback ─────────────────────────────────
                    var settlement = await _context.SettlementWithdrawals
                        .FirstOrDefaultAsync(x => x.PayoutTransactionId == txnId, cancellationToken);

                    if (settlement == null)
                    {
                        return Fail("Transaction not found");
                    }

                    return await CheckSettlementStatusAsync(settlement, cancellationToken);
                }

                // If already terminal, return DB state without calling API
                string dbStatus = tx.Status?.ToUpper() ?? "PENDING";
                if (dbStatus == "SUCCESS" || dbStatus == "FAILED" || dbStatus == "REFUNDED")
                {
                    return Fail("Transaction already processed");
                }

                // Call AeronPay status API
                string clientRefId = tx.TxnId;
                string dateOfTxn = tx.ReqDate.HasValue
                    ? tx.ReqDate.Value.ToString("dd-MM-yyyy")
                    : DateTime.Now.ToString("dd-MM-yyyy");

                var apiResponse = await CallAeronpayStatusApi(clientRefId, dateOfTxn, cancellationToken);

                if (apiResponse == null)
                {
                    return Fail("API Response not found");
                }

                string apiStatus = apiResponse.status?.ToUpper();
                string mappedStatus = apiStatus switch
                {
                    "SUCCESS" => "SUCCESS",
                    "FAILED" => "FAILED",
                    "PENDING" => "PENDING",
                    "ACCEPTED" => "PENDING",
                    _ => dbStatus
                };

                // Rate-limited or temp issue — don't change DB, return current
                if (apiStatus == "TOO_MANY_REQUESTS" || apiResponse.statusCode == "429" || apiResponse.statusCode == "444")
                {
                    return Fail("Too many requests please try again later");
                }

                // Update DB if status changed
                if (mappedStatus != dbStatus)
                {
                    tx.Status = mappedStatus;
                    tx.UpdateDate = DateTime.Now;
                    if (!string.IsNullOrEmpty(apiResponse.utr)) tx.Brid = apiResponse.utr;
                    if (!string.IsNullOrEmpty(apiResponse.transactionId)) tx.ApiTxnId = apiResponse.transactionId;
                    tx.ApiMsg = apiResponse.description ?? apiResponse.message;
                    await _context.SaveChangesAsync(cancellationToken);

                    var user = await _context.TblUsers.FirstOrDefaultAsync(x => x.Id == Convert.ToInt32(tx.UserId), cancellationToken);
                    if (user != null)
                    {
                        if (mappedStatus == "SUCCESS")
                        {
                            // Wallet was already debited at PENDING stage in MoneyTransfer — only distribute commission
                            await DistributeCommissionAsync(tx, user, Convert.ToDecimal(tx.Amount), user.CommissionPlanId ?? 1);
                            await _context.SaveChangesAsync(cancellationToken);
                        }
                        else if (mappedStatus == "FAILED")
                        {
                            decimal rtComm = Convert.ToDecimal(tx.Charge);
                            decimal totalRefund = Convert.ToDecimal(tx.Amount) + rtComm;

                            var (_, _, creditEntryId) = await _walletService.CreditAsync(
                                user.Id, user.Username + "-" + user.Phone,
                                tx.Amount ?? 0, totalRefund, rtComm, 0,
                                "Money_Transfer_Refund",
                                $"Money Transfer Refunded | DMT Payment For Account No {tx.AccountNo}| Credit by Services | Refund Credit TXN:{tx.TxnId}",
                                user.Wlid, cancellationToken);

                            bool creditVerified = creditEntryId > 0
                                && await _context.Tbluserbalances.AnyAsync(
                                       b => b.Id == creditEntryId && b.UserId == user.Id, cancellationToken);

                            if (!creditVerified)
                            {
                                tx.Status = "PENDING";
                                tx.UpdateDate = DateTime.Now;
                                try { await _context.SaveChangesAsync(CancellationToken.None); } catch { }
                                return Fail($"Refund credit not verified — TXN:{tx.TxnId} kept as PENDING for retry");
                            }
                        }
                    }
                }

                return SuccessResponse(tx, apiResponse.utr ?? tx.Brid ?? "", mappedStatus);
            }
            catch (Exception ex)
            {
                return Fail("ERR:500 " + ex.Message);
            }
        }

        private async Task<AeronpayStatusCheckResponse> CallAeronpayStatusApi(string clientReferenceId, string dateOfTransaction, CancellationToken cancellationToken)
        {
            try
            {
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

                var bodyObj = new AeronpayStatusCheckRequest
                {
                    client_referenceId = clientReferenceId,
                    mobile = _config.RegisteredMobile,
                    date_of_transaction = dateOfTransaction
                };

                string json = JsonConvert.SerializeObject(bodyObj);
                byte[] jsonBytes = Encoding.UTF8.GetBytes(json);

                var handler = new HttpClientHandler
                {
                    UseProxy = false,
                    ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true
                };

                using var client = new HttpClient(handler);
                var request = new HttpRequestMessage(HttpMethod.Post, _config.StatusCheckUrl);
                request.Headers.Add("client-id", _config.ClientId);
                request.Headers.Add("client-secret", _config.ClientSecret);
                request.Headers.Add("accept", "application/json");

                request.Content = new ByteArrayContent(jsonBytes);
                request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");

                var response = await client.SendAsync(request, cancellationToken);
                string resp = await response.Content.ReadAsStringAsync(cancellationToken);

                var log = new Apilog
                {
                    Apiname = "ARP-StatusCheck",
                    Reqdatae = DateTime.Now,
                    Request = json,
                    Response = resp
                };
                _context.Apilogs.Add(log);
                await _context.SaveChangesAsync(cancellationToken);

                return JsonConvert.DeserializeObject<AeronpayStatusCheckResponse>(resp);
            }
            catch
            {
                return null;
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

        private async Task<LoginModel> CheckSettlementStatusAsync(SettlementWithdrawal settlement, CancellationToken cancellationToken)
        {
            string dbStatus = settlement.PayoutStatus?.ToUpper() ?? "PENDING";

            if (dbStatus == "SUCCESS" || dbStatus == "FAILED")
            {
                return Fail($"Transaction already processed");
            }

            string dateOfTxn = settlement.CreatedAt.ToString("dd-MM-yyyy");
            var apiResponse = await CallAeronpayStatusApi(settlement.PayoutTransactionId, dateOfTxn, cancellationToken);

            if (apiResponse == null)
            {
                return Fail($"API Response not found");
            }

            string apiStatus = apiResponse.status?.ToUpper();

            if (apiStatus == "TOO_MANY_REQUESTS" || apiResponse.statusCode == "429" || apiResponse.statusCode == "444")
            {
                return Fail($"Too Many Requests please try again");
            } 

            string mappedStatus = apiStatus switch
            {
                "SUCCESS" => "SUCCESS",
                "FAILED"  => "FAILED",
                "PENDING" => "PENDING",
                "ACCEPTED" => "PENDING",
                _ => dbStatus
            };

            if (mappedStatus != dbStatus)
            {
                settlement.PayoutStatus  = mappedStatus;
                settlement.PayoutResponse = JsonConvert.SerializeObject(apiResponse);
                if (!string.IsNullOrEmpty(apiResponse.utr)) settlement.RRN = apiResponse.utr;
                await _context.SaveChangesAsync(cancellationToken);

                if (mappedStatus == "FAILED")
                {
                    var user = await _context.TblUsers
                        .FirstOrDefaultAsync(x => x.Id == Convert.ToInt32(settlement.UserId), cancellationToken);
                    if (user != null)
                    {
                        decimal totalRefund = settlement.Amount + settlement.Charge;
                        var (_, _, creditEntryId) = await _walletService.CreditAsync(
                            user.Id, user.Username + "-" + user.Phone,
                            settlement.Amount, totalRefund, settlement.Charge, 0,
                            "SettlementWithdrawal_Refund",
                            $"Refund for Failed Settlement Withdrawal for AccountNo: {settlement.BankAccount} | Credit by Services | Refund Credit TXN:{settlement.PayoutTransactionId}",
                            user.Wlid, cancellationToken);

                        bool creditVerified = creditEntryId > 0
                            && await _context.Tbluserbalances.AnyAsync(
                                   b => b.Id == creditEntryId && b.UserId == user.Id, cancellationToken);

                        if (!creditVerified)
                        {
                            settlement.PayoutStatus = "PENDING";
                            try { await _context.SaveChangesAsync(CancellationToken.None); } catch { }
                            return Fail($"Refund credit not verified — Settlement TXN:{settlement.PayoutTransactionId} kept as PENDING for retry");
                        }
                    }
                }
            }

            return SettlementSuccessResponse(settlement, apiResponse.utr ?? settlement.RRN ?? "", mappedStatus);
        }

        private LoginModel SettlementSuccessResponse(SettlementWithdrawal settlement, string rrn, string status)
        {
            var list = new List<DMTTXN>
            {
                new DMTTXN
                {
                    AccountNo       = settlement.BankAccount,
                    BeneName        = settlement.BeneName,
                    Amount          = settlement.Amount.ToString(),
                    Charge          = settlement.Charge.ToString(),
                    CurrentBalance  = "",
                    Status          = status,
                    TxnID           = settlement.PayoutTransactionId,
                    BR_Id           = rrn,
                    TxnDate         = settlement.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss.fff")
                }
            };

            return new LoginModel
            {
                Status_Code = status == "SUCCESS" ? "1" : "0",
                Message     = status,
                Data        = list
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

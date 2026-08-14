using InstantPay.Application.Interfaces;
using InstantPay.Application.Interfaces.MoneyTransfer.Tramo;
using InstantPay.Application.Interfaces.SMS;
using InstantPay.Infrastructure.Sql.Entities;
using InstantPay.SharedKernel.Entity.TramoConfigDTO;
using InstantPay.SharedKernel.RequestPayload.MoneyTransfer.AeronPay;
using InstantPay.SharedKernel.Results.MoneyTransfer.Castler;
using InstantPay.SharedKernel.Results.MoneyTransfer.Tramo;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace InstantPay.Application.Services.MoneyTransfer
{
    public class TramoUpiDmtService : ITramoUpiDmtService
    {
        private readonly TramoConfig _config;
        private readonly AppDbContext _context;
        private readonly IWalletService _walletService;
        private readonly ICommissionService _commissionService;
        private readonly ISmsService _smsService;

        private const int ServiceId = 6;
        private const string ApiCode = "TRAMO";

        public TramoUpiDmtService(
            IOptions<TramoConfig> config,
            AppDbContext context,
            IWalletService walletService,
            ICommissionService commissionService,
            ISmsService smsService)
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
            => inputPin.ToLower().Trim() == txnPin.ToLower().Trim();

        // Tramo status string → internal status
        // "Success" → SUCCESS (only when vendorUtrNumber is present)
        // "Failed"  → FAILED
        // Everything else → PENDING
        private static string MapTramoStatus(string? tramoStatus, string? vendorUtrNumber)
        {
            return (tramoStatus ?? "").ToLower() switch
            {
                "success" => string.IsNullOrWhiteSpace(vendorUtrNumber) ? "PENDING" : "SUCCESS",
                "failed"  => "FAILED",
                _         => "PENDING"
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

                TransactionDetail tx;
                decimal currentBalance;
                decimal rtComm;
                decimal totalDebit;
                decimal newBal;
                string clientReferenceId;
                int planId;

                await using var dbTx = await _context.Database.BeginTransactionAsync(cancellationToken);
                try
                {
                    string appLockName = $"DMT_{user.Id}_{model.AccountNumber}_{model.Amount}";
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

                    if (duplicate || (model.Amount < 1000 || model.Amount > 49000))
                    {
                        await dbTx.RollbackAsync(cancellationToken);
                        return Fail(duplicate ? "Duplicate Transaction" : "Amount should be in range of 1000-49000");
                    }

                    currentBalance = await _walletService.GetBalanceAsync(user.Id, cancellationToken);

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
                    clientReferenceId = "TRM" + DateTime.Now.ToString("yyyyMMddHHmmss") + apiRequestId.ToString();

                    var duplicateTxn = await _context.TransactionDetails.AnyAsync(
                        x => x.TxnId.ToLower().Trim() == clientReferenceId.ToLower().Trim(), cancellationToken);

                    if (duplicateTxn)
                    {
                        await dbTx.RollbackAsync(cancellationToken);
                        return Fail("Duplicate TxnId found, Please try again");
                    }

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
                        ApiName = ApiCode,
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
                        ServiceId = ServiceId,
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

                try
                {
                    var (_, _, debitEntryId) = await _walletService.DebitAsync(
                        user.Id, user.Name + "-" + user.Phone,
                        model.Amount ?? 0, totalDebit, rtComm, 0,
                        "Money_Transfer_Debit",
                        $"DMT Payment For Account No {tx.AccountNo}| Debit by Services | Amount Debit For DMT TxnId {tx.TxnId}",
                        user.Wlid, CancellationToken.None);

                    bool debitVerified = debitEntryId > 0
                        && await _context.Tbluserbalances.AnyAsync(
                               b => b.Id == debitEntryId && b.UserId == user.Id, CancellationToken.None);

                    if (!debitVerified)
                    {
                        tx.Status = "FAILED";
                        tx.ApiMsg = "Wallet debit failed before API call";
                        tx.UpdateDate = DateTime.Now;
                        try { await _context.SaveChangesAsync(CancellationToken.None); } catch
                        {
                            await _context.SaveChangesAsync(CancellationToken.None);
                        }
                        return Fail("Debit entry not found in tbluserbalance after insert");
                    }
                }
                catch
                {
                    tx.Status = "FAILED";
                    tx.ApiMsg = "Wallet debit failed before API call";
                    tx.UpdateDate = DateTime.Now;
                    try { await _context.SaveChangesAsync(CancellationToken.None); } catch
                    {
                        await _context.SaveChangesAsync(CancellationToken.None);
                    }
                    return Fail("Please try again later, there is an issue with your wallet");
                }

                var apiResponse = await CallTramoPayoutApi(model, clientReferenceId, cancellationToken);

                string status = "PENDING";
                string clientRefId = "";
                string vendorUtr = "";

                if (apiResponse != null && apiResponse.code == 200 && apiResponse.data != null)
                {
                    clientRefId = apiResponse.data.clientRefId ?? "";
                    vendorUtr = apiResponse.data.vendorUtrNumber ?? "";
                    status = MapTramoStatus(apiResponse.data.status, vendorUtr);

                    tx.ApiTxnId = !string.IsNullOrEmpty(clientRefId) ? clientRefId : clientReferenceId;
                    tx.ApiRes = JsonConvert.SerializeObject(apiResponse);
                    tx.ApiMsg = apiResponse.message ?? apiResponse.data.status;
                    tx.Brid = vendorUtr;
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
                    tx.Status = "PENDING";
                    tx.ApiRes = apiResponse != null ? JsonConvert.SerializeObject(apiResponse) : "API Timeout Error";
                    tx.ApiMsg = apiResponse?.message ?? "API call failed";
                    tx.UpdateDate = DateTime.Now;
                    await _context.SaveChangesAsync(CancellationToken.None);
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
                        BR_Id = vendorUtr,
                        TxnDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")
                    }
                };

                return new LoginModel
                {
                    Status_Code = status == "SUCCESS" || status == "PENDING" ? "1" : "0",
                    Message = status == "SUCCESS" || status == "PENDING" ? "Transaction Successful" : "Transaction Failed || " + (apiResponse?.message ?? ""),
                    Data = list
                };
            }
            catch (Exception ex)
            {
                return Fail("ERR:500 " + ex.Message);
            }
        }

        private async Task<TramoPayoutApiResponse> CallTramoPayoutApi(AeronpayDmtRequest model, string refId, CancellationToken cancellationToken)
        {
            try
            {
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

                bool isUpi = (model.AccountNumber ?? "").Contains("@");

                string upiAddress = isUpi ? model.AccountNumber : "";
                string accountNumber = isUpi ? "" : (model.AccountNumber ?? "");
                string ifsc = isUpi ? "" : (model.IFSC ?? "");
                string mode = isUpi ? "UPI" : "IMPS";

                var bodyObj = new
                {
                    amount = model.Amount?.ToString("0.##"),
                    remitterFirstName = _config.RemitterFirstName ?? "Instant Payment",
                    refId = refId,
                    beneName = model.BeneficiaryName,
                    remitterEmail = _config.RemitterEmail ?? "",
                    bankName = model.BankName ?? "",
                    accountNumber = accountNumber,
                    ifsc = ifsc,
                    remitterMobile = _config.RemitterMobile ?? "8684020633",
                    upiAddress = upiAddress,
                    payeeName = model.BeneficiaryName,
                    vpa = upiAddress,
                    email = _config.RemitterEmail ?? "",
                    phone = model.BeneficiaryMobile ?? "",
                    mode = mode,
                    remarks = model.Remark ?? "Payout Transaction",
                    latitude = _config.Latitude ?? "28.62838286093477",
                    longitude = _config.Longitude ?? "77.3778169692173",
                    beneAddress = _config.BeneAddress ?? "India",
                    paymentType = _config.PaymentType ?? "VENDOR_SETTLEMENT",
                    beneficiaryId = ""
                };

                string json = JsonConvert.SerializeObject(bodyObj);
                byte[] jsonBytes = Encoding.UTF8.GetBytes(json);

                var handler = new SocketsHttpHandler
                {
                    UseProxy = false,
                    AutomaticDecompression = DecompressionMethods.None,
                    ConnectCallback = async (context, ct) =>
                    {
                        var addresses = await Dns.GetHostAddressesAsync(context.DnsEndPoint.Host, ct);
                        var ipv4 = addresses.First(a => a.AddressFamily == AddressFamily.InterNetwork);
                        var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                        await socket.ConnectAsync(ipv4, context.DnsEndPoint.Port, ct);
                        return new NetworkStream(socket, ownsSocket: true);
                    }
                };

                using var client = new HttpClient(handler)
                {
                    Timeout = TimeSpan.FromMinutes(5)
                };

                var request = new HttpRequestMessage(HttpMethod.Post, _config.PayoutUrl);
                request.Content = new ByteArrayContent(jsonBytes);
                request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
                request.Headers.Add("Token", _config.ApiKey);

                var response = await client.SendAsync(request, cancellationToken);
                string resp = await response.Content.ReadAsStringAsync(cancellationToken);

                var log = new Apilog
                {
                    Apiname = "Transaction-TRAMO",
                    Reqdatae = DateTime.Now,
                    Request = json,
                    Response = resp
                };
                _context.Apilogs.Add(log);
                await _context.SaveChangesAsync(cancellationToken);

                return JsonConvert.DeserializeObject<TramoPayoutApiResponse>(resp);
            }
            catch (Exception ex)
            {
                var log = new Apilog
                {
                    Apiname = "Transaction-TRAMO-ERROR",
                    Reqdatae = DateTime.Now,
                    Request = JsonConvert.SerializeObject(model),
                    Response = ex.ToString()
                };
                _context.Apilogs.Add(log);
                await _context.SaveChangesAsync();
                return null;
            }
        }

        private async Task<bool> EnsureDebitForSuccessAsync(TransactionDetail tx, TblUser user, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(tx.TxnId) || tx.Amount == null)
                return false;

            decimal rtComm = Convert.ToDecimal(tx.Charge);
            decimal totalDebit = Convert.ToDecimal(tx.Amount) + rtComm;

            if (totalDebit <= 0)
                return false;

            decimal debitSum = await _context.Tbluserbalances
                .Where(b => b.UserId == user.Id &&
                            b.TxnType == "Money_Transfer_Debit" &&
                            b.Remarks != null &&
                            b.Remarks.Contains(tx.TxnId))
                .SumAsync(b => b.Amount ?? 0m, cancellationToken);

            decimal refundSum = await _context.Tbluserbalances
                .Where(b => b.UserId == user.Id &&
                            b.TxnType == "Money_Transfer_Refund" &&
                            b.Remarks != null &&
                            b.Remarks.Contains(tx.TxnId))
                .SumAsync(b => b.Amount ?? 0m, cancellationToken);

            decimal netDebit = debitSum - refundSum;
            if (netDebit >= totalDebit)
                return true;

            decimal missingDebit = totalDebit - netDebit;
            decimal reDebitSurComm = missingDebit * rtComm / totalDebit;
            decimal reDebitTxnAmount = missingDebit - reDebitSurComm;

            var (oldBal, newBal, debitEntryId) = await _walletService.DebitAsync(
                user.Id, user.Name + "-" + user.Phone,
                reDebitTxnAmount, missingDebit, reDebitSurComm, 0,
                "Money_Transfer_Debit",
                $"DMT Payment For Account No {tx.AccountNo}| Debit by Services | Amount Debit For DMT TxnId {tx.TxnId}",
                user.Wlid, cancellationToken);

            bool debitVerified = debitEntryId > 0
                && await _context.Tbluserbalances.AnyAsync(
                       b => b.Id == debitEntryId && b.UserId == user.Id, cancellationToken);

            if (debitVerified)
            {
                tx.OldBal = oldBal;
                tx.NewBal = Convert.ToString(newBal);
            }

            return debitVerified;
        }

        public async Task<LoginModel> CheckStatus(string txnId, CancellationToken cancellationToken)
        {
            try
            {
                var tx = await _context.TransactionDetails
                    .FirstOrDefaultAsync(x => x.TxnId == txnId || x.ApiTxnId == txnId, cancellationToken);

                if (tx == null)
                    return Fail("Transaction not found");

                string dbStatus = tx.Status?.ToUpper() ?? "PENDING";
                if (dbStatus == "SUCCESS")
                    return Fail("Transaction already processed");

                if (!string.IsNullOrWhiteSpace(_config.StatusCheckUrl))
                {
                    // Use Tramo's clientRefId (stored in ApiTxnId after first call) for check-status
                    string tramoClientRefId = !string.IsNullOrEmpty(tx.ApiTxnId) && tx.ApiTxnId != tx.TxnId
                        ? tx.ApiTxnId
                        : "";

                    var apiResponse = await CallTramoStatusApi(tramoClientRefId, tx.TxnId, cancellationToken);

                    if (apiResponse == null)
                        return Fail("API Response not found");

                    if (apiResponse.code != 200 || apiResponse.data == null)
                        return Fail(apiResponse.message ?? "Status check failed");

                    string vendorUtr = apiResponse.data.vendorUtrNumber ?? "";
                    string mappedStatus = MapTramoStatus(apiResponse.data.status, vendorUtr);

                    var user = await _context.TblUsers.FirstOrDefaultAsync(x => x.Id == Convert.ToInt32(tx.UserId), cancellationToken);
                    if (user == null)
                        return Fail("User not found");

                    if (mappedStatus == "SUCCESS")
                    {
                        bool debitVerified = await EnsureDebitForSuccessAsync(tx, user, cancellationToken);
                        if (!debitVerified)
                            return Fail($"Wallet debit not verified — TXN:{tx.TxnId} kept as {dbStatus} for retry");

                        tx.Status = "SUCCESS";
                        tx.UpdateDate = DateTime.Now;
                        if (!string.IsNullOrEmpty(vendorUtr)) tx.Brid = vendorUtr;
                        if (!string.IsNullOrEmpty(apiResponse.data.clientRefId)) tx.ApiTxnId = apiResponse.data.clientRefId;
                        tx.ApiMsg = apiResponse.message ?? apiResponse.data.status;
                        await _context.SaveChangesAsync(cancellationToken);

                        await DistributeCommissionAsync(tx, user, Convert.ToDecimal(tx.Amount), user.CommissionPlanId ?? 1);
                        await _context.SaveChangesAsync(cancellationToken);
                    }
                    else if (mappedStatus == "FAILED")
                    {
                        if (dbStatus != "FAILED" && dbStatus != "REFUNDED")
                        {
                            tx.Status = "FAILED";
                            tx.UpdateDate = DateTime.Now;
                            if (!string.IsNullOrEmpty(vendorUtr)) tx.Brid = vendorUtr;
                            if (!string.IsNullOrEmpty(apiResponse.data.clientRefId)) tx.ApiTxnId = apiResponse.data.clientRefId;
                            tx.ApiMsg = apiResponse.message ?? apiResponse.data.status;
                            await _context.SaveChangesAsync(cancellationToken);

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
                    else if (mappedStatus == "PENDING" && dbStatus != "FAILED" && dbStatus != "REFUNDED")
                    {
                        tx.Status = "PENDING";
                        tx.UpdateDate = DateTime.Now;
                        if (!string.IsNullOrEmpty(vendorUtr)) tx.Brid = vendorUtr;
                        if (!string.IsNullOrEmpty(apiResponse.data.clientRefId)) tx.ApiTxnId = apiResponse.data.clientRefId;
                        tx.ApiMsg = apiResponse.message ?? apiResponse.data.status;
                        await _context.SaveChangesAsync(cancellationToken);
                    }

                    return SuccessResponse(tx, vendorUtr.Length > 0 ? vendorUtr : (tx.Brid ?? ""), tx.Status ?? mappedStatus);
                }

                return SuccessResponse(tx, tx.Brid ?? "", tx.Status ?? dbStatus);
            }
            catch (Exception ex)
            {
                return Fail("ERR:500 " + ex.Message);
            }
        }

        private async Task<TramoStatusCheckResponse> CallTramoStatusApi(string clientRefId, string partnerTransactionId, CancellationToken cancellationToken)
        {
            try
            {
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

                var bodyObj = new
                {
                    clientRefId = clientRefId,
                    partnerTransactionId = partnerTransactionId
                };

                string json = JsonConvert.SerializeObject(bodyObj);
                byte[] jsonBytes = Encoding.UTF8.GetBytes(json);

                var handler = new SocketsHttpHandler
                {
                    UseProxy = false,
                    AutomaticDecompression = DecompressionMethods.None,
                    ConnectCallback = async (context, ct) =>
                    {
                        var addresses = await Dns.GetHostAddressesAsync(context.DnsEndPoint.Host, ct);
                        var ipv4 = addresses.First(a => a.AddressFamily == AddressFamily.InterNetwork);
                        var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                        await socket.ConnectAsync(ipv4, context.DnsEndPoint.Port, ct);
                        return new NetworkStream(socket, ownsSocket: true);
                    }
                };

                using var client = new HttpClient(handler);
                var request = new HttpRequestMessage(HttpMethod.Post, _config.StatusCheckUrl);
                request.Content = new ByteArrayContent(jsonBytes);
                request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
                request.Headers.Add("Token", _config.ApiKey);

                var response = await client.SendAsync(request, cancellationToken);
                string resp = await response.Content.ReadAsStringAsync(cancellationToken);

                var log = new Apilog
                {
                    Apiname = "TRAMO-StatusCheck",
                    Reqdatae = DateTime.Now,
                    Request = json,
                    Response = resp
                };
                _context.Apilogs.Add(log);
                await _context.SaveChangesAsync(cancellationToken);

                return JsonConvert.DeserializeObject<TramoStatusCheckResponse>(resp);
            }
            catch
            {
                return null;
            }
        }

        private LoginModel Fail(string msg)
            => new LoginModel { Status_Code = "0", Message = msg, Data = null };

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

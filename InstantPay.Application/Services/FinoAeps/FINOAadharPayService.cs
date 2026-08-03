using InstantPay.Application.Interfaces;
using InstantPay.Application.Interfaces.FinoAeps;
using InstantPay.Infrastructure.Sql.Entities;
using InstantPay.SharedKernel.RequestPayload.FinoAEPS;
using InstantPay.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace InstantPay.Application.Services.FinoAeps
{
    public class FINOAadharPayService : IFINOAadharPayService
    {
        private readonly IFinoAepsApiClient _api;
        private readonly IFinoAepsTransactionService _txnService;
        private readonly IWalletService _walletService;
        private readonly ILogger<FINOAadharPayService> _logger;
        private readonly AppDbContext _context;

        public FINOAadharPayService(
            IFinoAepsApiClient api,
            IFinoAepsTransactionService txnService,
            IWalletService walletService,
            ILogger<FINOAadharPayService> logger,
            AppDbContext context)
        {
            _api                = api;
            _txnService         = txnService;
            _walletService      = walletService;
            _logger             = logger;
            _context            = context;
        }

        public async Task<FinoAepsResponse> ProcessAsync(
            FinoAepsRequest request, string userId, string txnId,
            string lat, string lng, CancellationToken ct = default)
        {
            string amount = string.IsNullOrWhiteSpace(request.amount) ? "0" : request.amount!;
            decimal txnAmount = decimal.TryParse(amount, out var a) ? a : 0m;

            string isNpciOtp = txnAmount > 5000m ? "1" : "0";
            if (isNpciOtp == "1" &&
                (string.IsNullOrWhiteSpace(request.npciTxnId) || string.IsNullOrWhiteSpace(request.npciTxnRefNo)))
                return Err("NPCI TransactionId and TxnReferenceNo are required for amounts above 5000");

            string bodyJson = JsonConvert.SerializeObject(new
            {
                MerchantID  = request.mobileno,
                Version     = "1001",
                ServiceID   = "176",
                ClientRefID = txnId,
                MobileNo    = request.mobileno,
                AadharNo    = request.aadharno,
                TotalAmount = amount,
                BankName    = request.BankName,
                PidData     = request.fingerdata,
                RC          = "Y",
                NBIN        = request.bankiinno,
                TerminalId  = request.mobileno,
                IPAddress   = _api.ProdIPAddress,
                Latitude    = lat,
                Longitude   = lng,
                IMEI_MAC       = _api.GetMacAddress(),
                DeviceNo       = request.DeviceSrNo,
                CheckSum       = _api.ComputeChecksum($"{txnId}+{amount}+{request.aadharno}"),
                IsIris         = request.deviceType,
                MerAuthTxnId   = request.merAuthTxnId ?? "",
                IsNpciOtp      = isNpciOtp,
                TransactionId  = request.npciTxnId ?? "",
                TxnReferenceNo = request.npciTxnRefNo ?? ""
            });

            decimal charge;
            if (txnAmount >= 100 && txnAmount <= 1000)
                charge = 3;
            else if (txnAmount >= 1001 && txnAmount <= 10000)
                charge = txnAmount * 0.35m / 100;
            else
                charge = 0;

            if (!int.TryParse(userId, out int uid) || uid <= 0)
                return Err("Invalid user");

            var user = await _context.TblUsers
                .Where(u => u.Id == uid)
                .Select(u => new { u.Name, u.Phone, u.Wlid })
                .FirstOrDefaultAsync(ct);

            if (user == null)
                return Err("User not found");

            decimal currentBalance = await _walletService.GetBalanceAsync(uid, ct);
            if (currentBalance < charge)
                return Err("Insuficient Balance, to made this transaction you should have minimum balance in wallet: " + charge + ".");

            decimal preNewBal = currentBalance - charge;

            var pendingRec = new FinoAepsTxnRecord(
                UserId      : uid,
                TxnId       : txnId,
                Mobile      : request.customermobileno ?? request.mobileno,
                Amount      : txnAmount,
                Status      : "Pending",
                TxnType     : "AP",
                Rrn         : "",
                AadhaarNo   : request.aadharno,
                BankName    : request.BankName,
                Brid        : txnId,
                ApiMsg      : "",
                ApiRes      : "",
                ApiReq      : bodyJson,
                Comm        : 0,
                MdComm      : 0,
                AdComm      : 0,
                WlComm      : 0,
                Tds         : 0,
                Cost        : txnAmount + charge,
                NewBal      : preNewBal,
                IfscCode    : null,
                CustomerName: request.customermobileno ?? request.mobileno,
                OpId        : null,
                OperatorName: "FINO_AEPS_AADHAR_PAY",
                ComingFrom  : request.comingFrom,
                charge      : charge
            );

            bool pendingInserted = await _txnService.InsertPendingAsync(pendingRec, ct);
            if (!pendingInserted)
                return Err("Failed to insert pending transaction");

            decimal debitNewBal  = 0;
            int     debitEntryId = 0;
            try
            {
                (_, debitNewBal, debitEntryId) = await _walletService.DebitAsync(
                    uid, $"{user.Name}-{user.Phone}",
                    charge, charge, charge, 0,
                    "AEPS AP Charge",
                    $"AEPS AadharPay Charge | Bank: {request.BankName} | TxnId: {txnId}",
                    user.Wlid, CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "AP wallet debit failed for UserId={UserId} TxnId={TxnId}", uid, txnId);
                return Err("Error, please try again");
            }

            bool debitVerified = debitEntryId > 0
                && await _context.Tbluserbalances.AnyAsync(
                       b => b.Id == debitEntryId && b.UserId == uid, CancellationToken.None);

            if (!debitVerified)
            {
                await UpdateTxnAsync(txnId, "FAILED", null, "Wallet debit failed before API call",
                                     currentBalance, "", CancellationToken.None);
                return Err("Wallet debit failed before API call");
            }

            var result = await _api.PostAadharPayProdAsync(bodyJson, ct);

            string status = result.IsSuccess
                ? (result.DecryptedData?["Status"]?.ToString() ?? "SUCCESS")
                : "FAILED";
            string rrn    = result.DecryptedData?["RRN"]?.ToString() ?? "NA";
            string apiMsg = result.IsSuccess
                ? (result.DecryptedData?["MessageString"]?.ToString() ?? result.MessageString)
                : result.MessageString;

            bool isSuccessOrPending = IsSuccessOrPending(status);
            decimal finalNewBal = debitNewBal;

            if (!result.IsSuccess || !isSuccessOrPending)
            {
                status = "FAILED";

                try
                {
                    var (_, refundNewBal, refundEntryId) = await _walletService.CreditAsync(
                        uid, $"{user.Name}-{user.Phone}",
                        txnAmount, charge, charge, 0,
                        "AEPS AP Refund",
                        $"AEPS AadharPay Refund | Bank: {request.BankName} | TxnId: {txnId}",
                        user.Wlid, CancellationToken.None);

                    bool refundVerified = refundEntryId > 0
                        && await _context.Tbluserbalances.AnyAsync(
                               b => b.Id == refundEntryId && b.UserId == uid, CancellationToken.None);

                    if (!refundVerified)
                    {
                        await UpdateTxnAsync(txnId, "PENDING", result.RawResponse,
                            "Refund credit not verified — kept as PENDING for retry",
                            debitNewBal, rrn, CancellationToken.None);
                        return Err("Refund credit not verified");
                    }

                    finalNewBal = refundNewBal;
                    await UpdateTxnAsync(txnId, "FAILED", result.RawResponse, apiMsg,
                                         finalNewBal, rrn, CancellationToken.None);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "AP refund failed for UserId={UserId} TxnId={TxnId}", uid, txnId);
                    await UpdateTxnAsync(txnId, "PENDING", result.RawResponse,
                        "Refund credit failed — kept as PENDING for retry",
                        debitNewBal, rrn, CancellationToken.None);
                    return Err("Refund failed");
                }
            }
            else
            {
                try
                {
                    var (_, creditNewBal, creditEntryId) = await _walletService.CreditAsync(
                        uid, $"{user.Name}-{user.Phone}",
                        txnAmount, txnAmount, 0, 0,
                        "AEPS AP",
                        $"AEPS AadharPay | Bank: {request.BankName} | TxnId: {txnId}",
                        user.Wlid, CancellationToken.None);

                    bool creditVerified = creditEntryId > 0
                        && await _context.Tbluserbalances.AnyAsync(
                               b => b.Id == creditEntryId && b.UserId == uid, CancellationToken.None);

                    if (!creditVerified)
                    {
                        await UpdateTxnAsync(txnId, "PENDING", result.RawResponse,
                            "Wallet credit not verified — kept as PENDING for retry",
                            debitNewBal, rrn, ct);
                        return Err("Wallet credit not verified");
                    }

                    finalNewBal = creditNewBal;
                    await UpdateTxnAsync(txnId, status, result.RawResponse, apiMsg,
                                         finalNewBal, rrn, ct);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "AP wallet credit failed for UserId={UserId} TxnId={TxnId}", uid, txnId);
                    await UpdateTxnAsync(txnId, "PENDING", result.RawResponse,
                        "Wallet credit failed — kept as PENDING for retry",
                        debitNewBal, rrn, ct);
                    return Err("Wallet credit failed");
                }
            }

            if (!result.IsSuccess)
                return Err(result.MessageString);

            var d = result.DecryptedData!;
            return Ok(result.MessageString, new List<FinoBalanceEnquiryData>
            {
                new()
                {
                    AdhaarNo         = d["AdhaarNo"]?.ToString() ?? "",
                    BankName         = d["BankName"]?.ToString() ?? "",
                    UTRNO            = d["RRN"]?.ToString() ?? "",
                    Status           = d["Status"]?.ToString() ?? "",
                    CustomerMobile   = d["CustomerMobile"]?.ToString() ?? "",
                    Amount           = d["Amount"]?.ToString() ?? "",
                    AvailableBalance = d["AvailableBalance"]?.ToString() ?? "",
                    TxnDate          = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")
                }
            });
        }

        private async Task UpdateTxnAsync(
            string txnId, string status, string? rawResponse, string? apiMsg,
            decimal newBal, string rrn, CancellationToken ct)
        {
            var txn = await _context.TransactionDetails
                .FirstOrDefaultAsync(t => t.TxnId == txnId, ct);

            if (txn == null) return;

            txn.Status     = status;
            txn.UpdateDate = DateTime.Now;
            if (rawResponse != null) txn.ApiRes = Truncate(rawResponse, 4000);
            if (apiMsg != null)      txn.ApiMsg = Truncate(apiMsg, 500);
            txn.NewBal     = Convert.ToString(newBal);
            txn.Brid       = rrn;

            await _context.SaveChangesAsync(ct);
        }

        private static bool IsSuccessOrPending(string status)
        {
            return status.Equals("Success", StringComparison.OrdinalIgnoreCase)
                || status.Equals("SUCCESS", StringComparison.OrdinalIgnoreCase)
                || status.Equals("Pending", StringComparison.OrdinalIgnoreCase)
                || status.Equals("PENDING", StringComparison.OrdinalIgnoreCase);
        }

        private static string? Truncate(string? s, int max)
            => s == null ? null : s.Length <= max ? s : s[..max];

        private static FinoAepsResponse Err(string msg) => new() { Status_Code = "0", Message = msg, Data = msg };
        private static FinoAepsResponse Ok(string msg, object data) => new() { Status_Code = "1", Message = msg, Data = data };
    }
}

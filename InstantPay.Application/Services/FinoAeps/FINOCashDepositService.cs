using InstantPay.Application.Interfaces;
using InstantPay.Application.Interfaces.FinoAeps;
using InstantPay.Infrastructure.Sql.Entities;
using InstantPay.SharedKernel.RequestPayload.FinoAEPS;
using InstantPay.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace InstantPay.Application.Services.FinoAeps
{
    public class FINOCashDepositService : IFINOCashDepositService
    {
        private readonly IFinoAepsApiClient _api;
        private readonly IFinoAepsTransactionService _txnService;
        private readonly IWalletService _walletService;
        private readonly IFinoAepsCommissionService _commissionService;
        private readonly AppDbContext _context;
        private readonly ILogger<FINOCashDepositService> _logger;

        private readonly string _cdUrl;

        public FINOCashDepositService(
            IFinoAepsApiClient api,
            IFinoAepsTransactionService txnService,
            IWalletService walletService,
            IFinoAepsCommissionService commissionService,
            AppDbContext context,
            IConfiguration config,
            ILogger<FINOCashDepositService> logger)
        {
            _api                = api;
            _txnService         = txnService;
            _walletService      = walletService;
            _commissionService  = commissionService;
            _context            = context;
            _logger             = logger;

            _cdUrl = config["FinoAEPS:Prod:CashDepositUrl"]
                     ?? "https://fpbs.fino.bank.in/AEPSAPIService/AEPSUIService.svc/ProcessRequest/AEPSCDA";
        }

        public async Task<FinoAepsResponse> ProcessAsync(
            FinoAepsRequest request, string userId, string txnId,
            string lat, string lng, CancellationToken ct = default)
        {
            string amount = string.IsNullOrWhiteSpace(request.amount) ? "0" : request.amount!;

            string bodyJson = JsonConvert.SerializeObject(new
            {
                MerchantID  = request.mobileno,
                Version     = "1001",
                ServiceID   = "150",
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
                IMEI_MAC    = _api.GetMacAddress(),
                DeviceNo    = request.DeviceSrNo,
                CheckSum    = _api.ComputeChecksum($"{txnId}+{amount}+{request.aadharno}"),
                IsIris      = request.deviceType
            });

            decimal txnAmount = decimal.TryParse(amount, out var a) ? a : 0m;

            if (!int.TryParse(userId, out int uid) || uid <= 0)
                return Err("Invalid user");

            var commission = await _commissionService.CalculateCommissionAsync(uid, txnAmount, "CD", ct);

            decimal charge       = commission.RetailerCommission;
            decimal totalDebit   = txnAmount + charge;

            var user = await _context.TblUsers
                .Where(u => u.Id == uid)
                .Select(u => new { u.Name, u.Phone, u.Wlid })
                .FirstOrDefaultAsync(ct);

            if (user == null)
                return Err("User not found");

            decimal currentBalance = await _walletService.GetBalanceAsync(uid, ct);
            if (currentBalance < totalDebit)
                return Err("Insufficient Balance");

            decimal preNewBal = currentBalance - totalDebit;

            var pendingRec = new FinoAepsTxnRecord(
                UserId      : uid,
                TxnId       : txnId,
                Mobile      : request.customermobileno ?? request.mobileno,
                Amount      : txnAmount,
                Status      : "Pending",
                TxnType     : "CD",
                Rrn         : "",
                AadhaarNo   : request.aadharno,
                BankName    : request.BankName,
                Brid        : txnId,
                ApiMsg      : "",
                ApiRes      : "",
                ApiReq      : bodyJson,
                Comm        : commission.RetailerCommission,
                MdComm      : commission.MdCommission,
                AdComm      : commission.AdCommission,
                WlComm      : commission.WlCommission,
                Tds         : commission.Tds,
                Cost        : commission.Cost,
                NewBal      : preNewBal,
                IfscCode    : null,
                CustomerName: null,
                OpId        : null,
                OperatorName: "FINO_AEPS_CASH_DEPOSIT",
                ComingFrom  : request.comingFrom
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
                    txnAmount, totalDebit, charge, 0,
                    "AEPS CD",
                    $"AEPS Cash Deposit | Bank: {request.BankName} | TxnId: {txnId}",
                    user.Wlid, CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "CD wallet debit failed for UserId={UserId} TxnId={TxnId}", uid, txnId);
                return Err("API Error");
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

            var result = await _api.PostProdAsync(_cdUrl, bodyJson, ct);

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
                        txnAmount, totalDebit, charge, 0,
                        "AEPS CD Refund",
                        $"AEPS Cash Deposit Refund | Bank: {request.BankName} | TxnId: {txnId}",
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
                    _logger.LogWarning(ex, "CD refund failed for UserId={UserId} TxnId={TxnId}", uid, txnId);
                    await UpdateTxnAsync(txnId, "PENDING", result.RawResponse,
                        "Refund credit failed — kept as PENDING for retry",
                        debitNewBal, rrn, CancellationToken.None);
                    return Err("Refund failed");
                }
            }
            else
            {
                await UpdateTxnAsync(txnId, status, result.RawResponse, apiMsg,
                                     debitNewBal, rrn, ct);
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

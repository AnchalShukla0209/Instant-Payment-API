// ── FINO AEPS ORCHESTRATOR ───────────────────────────────────────────────────
// Validates the incoming request (APIKey + SessionKey), resolves lat/long,
// generates a TxnId, then delegates to the appropriate per-txnType service.
// All encryption, HTTP, DB, and wallet logic lives in the dedicated services.
// ─────────────────────────────────────────────────────────────────────────────
using InstantPay.Application.Interfaces;
using InstantPay.Application.Interfaces.FinoAeps;
using InstantPay.Infrastructure.Sql.Entities;
using InstantPay.SharedKernel.Entity;
using InstantPay.SharedKernel.RequestPayload.FinoAEPS;
using InstantPay.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Concurrent;

namespace InstantPay.Application.Services
{
    public class FinoAepsService : IFinoAepsService
    {
        private readonly AppDbContext _context;
        private readonly IFinoAepsApiClient _apiClient;
        private readonly IInstantPayLogService _logService;
        private readonly IWalletService _walletService;
        private readonly IFinoAepsWalletService _finoWalletService;
        private readonly ILogger<FinoAepsService> _logger;
        private readonly string _validApiKey;
        private readonly string _transactionEnquiryUrl;
        private static readonly ConcurrentDictionary<string, SemaphoreSlim> EnquiryLocks = new();

        private readonly IFINOBalanceEnquiryService  _balanceEnquiry;
        private readonly IFINOCashWithdrawalService  _cashWithdrawal;
        private readonly IFINOMiniStatementService   _miniStatement;
        private readonly IFINOCashDepositService     _cashDeposit;
        private readonly IFINOAadharPayService       _aadharPay;
        private readonly IFINODailyLoginService      _dailyLogin;
        private readonly IFINORegistrationService    _registration;
        private readonly IFINOMerchantAuthService    _merchantAuth;
        private readonly IFINONpciOtpService         _npciOtp;

        public FinoAepsService(
            AppDbContext context,
            IFinoAepsApiClient apiClient,
            IInstantPayLogService logService,
            IWalletService walletService,
            IFinoAepsWalletService finoWalletService,
            IConfiguration config,
            ILogger<FinoAepsService> logger,
            IFINOBalanceEnquiryService balanceEnquiry,
            IFINOCashWithdrawalService cashWithdrawal,
            IFINOMiniStatementService  miniStatement,
            IFINOCashDepositService    cashDeposit,
            IFINOAadharPayService      aadharPay,
            IFINODailyLoginService     dailyLogin,
            IFINORegistrationService   registration,
            IFINOMerchantAuthService   merchantAuth,
            IFINONpciOtpService        npciOtp)
        {
            _context        = context;
            _apiClient      = apiClient;
            _logService     = logService;
            _walletService  = walletService;
            _finoWalletService = finoWalletService;
            _logger         = logger;
            _validApiKey    = config["FinoAEPS:ValidApiKey"] ?? "FinoAEPS001";
            _transactionEnquiryUrl = config["FinoAEPS:TransactionEnquiryUrl"]
                ?? "https://fpbs.fino.bank.in/PaymentBankBCAPI/UIService.svc/AEPSTransactionEnquiry";
            _balanceEnquiry = balanceEnquiry;
            _cashWithdrawal = cashWithdrawal;
            _miniStatement  = miniStatement;
            _cashDeposit    = cashDeposit;
            _aadharPay      = aadharPay;
            _dailyLogin     = dailyLogin;
            _registration   = registration;
            _merchantAuth   = merchantAuth;
            _npciOtp        = npciOtp;
        }

        // ── MAIN ENTRY POINT ─────────────────────────────────────────
        public async Task<FinoAepsResponse> ProcessAsync(FinoAepsRequest request, CancellationToken ct = default)
        {
            string rawRequest = JsonConvert.SerializeObject(request);
            await _logService.AddLogAsync(rawRequest, rawRequest, "FINO_AEPS_IN");

            try
            {
                // Step 1 – Validate API key
                if (string.IsNullOrWhiteSpace(request.APIKey) || request.APIKey != _validApiKey)
                    return Err("3", "Please Update Your App From Playstore");

                // Step 2 – Decrypt SessionKey → extract username
                string username;
                try
                {
                    string dec = _apiClient.DecryptSessionKey(request.SessionKey ?? "");
                    username = dec.Split('#')[0];
                    if (string.IsNullOrWhiteSpace(username)) throw new InvalidDataException("empty username");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "SessionKey decrypt failed");
                    return Err("2", "Session Expired Please Login Again");
                }

                // Step 3 – Validate session in DB
                string? userId = await ValidateSessionAsync(username, ct);
                if (string.IsNullOrEmpty(userId))
                    return Err("2", "Session Expired Please Login Again");

                // Step 4 – Resolve lat/long (fallback to DB value if not in request)
                string lat = request.latitude ?? "";
                string lng = request.longitude ?? "";
                if (string.IsNullOrEmpty(lat) || string.IsNullOrEmpty(lng))
                {
                    var ll = await GetUserLatLongAsync(username, request.SessionKey!, ct);
                    lat = ll.Lat ?? "";
                    lng = ll.Lng ?? "";
                }
                if (string.IsNullOrEmpty(lat) || string.IsNullOrEmpty(lng))
                    return Err("0", "Invalid Lat Long! Please Update your APP!");

                // Step 5 – Generate unique TxnId and dispatch to per-txnType service
                string txnId   = _apiClient.GenerateTxnId();
                string txnType = (request.txntype ?? "").ToLower().Trim();

                return txnType switch
                {
                    "be"       => await _balanceEnquiry.ProcessAsync(request, userId, txnId, lat, lng, ct),
                    "cw"       => await _cashWithdrawal.ProcessAsync(request, userId, txnId, lat, lng, ct),
                    "ms"       => await _miniStatement .ProcessAsync(request, userId, txnId, lat, lng, ct),
                    "cd"       => await _cashDeposit   .ProcessAsync(request, userId, txnId, lat, lng, ct),
                    "ap"       => await _aadharPay     .ProcessAsync(request, userId, txnId, lat, lng, ct),
                    "dl"       => await _dailyLogin    .ProcessAsync(request, userId, txnId, lat, lng, ct),
                    "reg"      => await _registration  .ProcessAsync(request, userId, txnId, lat, lng, ct),
                    "ma"       => await _merchantAuth  .ProcessAsync(request, userId, txnId, lat, lng, ct),
                    "merchantauth" => await _merchantAuth.ProcessAsync(request, userId, txnId, lat, lng, ct),
                    "npciotp"  => await _npciOtp       .ProcessAsync(request, userId, txnId, lat, lng, ct),
                    _          => await _balanceEnquiry.ProcessAsync(request, userId, txnId, lat, lng, ct)
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "FinoAepsService.ProcessAsync unhandled error");
                await _logService.AddLogAsync(rawRequest, ex.ToString(), "FINO_AEPS_ERROR");
                return Err("0", ex.Message);
            }
        }

        public async Task<FinoAepsResponse> CheckTransactionStatusAsync(FinoAepsTransactionStatusRequest request, CancellationToken ct = default)
        {
            string lockKey = request.ClientRefID?.Trim() ?? "";
            var enquiryLock = EnquiryLocks.GetOrAdd(lockKey, _ => new SemaphoreSlim(1, 1));
            await enquiryLock.WaitAsync(ct);
            try
            {
                return await CheckTransactionStatusCoreAsync(request, ct);
            }
            finally
            {
                enquiryLock.Release();
            }
        }

        private async Task<FinoAepsResponse> CheckTransactionStatusCoreAsync(FinoAepsTransactionStatusRequest request, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(request.APIKey) || request.APIKey != _validApiKey)
                return Err("3", "Please Update Your App From Playstore");
            if (string.IsNullOrWhiteSpace(request.ClientRefID))
                return Err("0", "ClientRefID is required");

            string username;
            try
            {
                username = request.userid;
                if (string.IsNullOrWhiteSpace(username)) throw new InvalidDataException("empty username");
            }
            catch
            {
                return Err("2", "Session Expired Please Login Again");
            }

            string? userId = await ValidateSessionAsync(username, ct);
            if (string.IsNullOrEmpty(userId))
                return Err("2", "Session Expired Please Login Again");

            var txn = await _context.TransactionDetails.FirstOrDefaultAsync(t =>
                t.TxnId == request.ClientRefID
                && t.ServiceName == "AEPS"
                && t.ApiName == "FINO", ct);

            if (txn == null)
                return Err("0", "Transaction not found");

            string currentStatus = txn.Status?.ToUpperInvariant() ?? "PENDING";
            if (currentStatus is "SUCCESS" or "FAILED")
                return Err("0", "Transaction already processed");

            const string serviceId = "159";
            const string version = "1001";
            string checksum = _apiClient.ComputeChecksum(request.ClientRefID + serviceId + version);
            string bodyJson = JsonConvert.SerializeObject(new
            {
                SERVICEID = serviceId,
                Version = version,
                ClientRefID = request.ClientRefID,
                CheckSum = checksum
            });

            FinoApiCallResult result;
            try
            {
                result = await _apiClient.PostTransactionEnquiryAsync(_transactionEnquiryUrl, bodyJson, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "FINO transaction enquiry failed for TxnId={TxnId}", txn.TxnId);
                return Err("0", "Transaction status failed. Please try again");
            }

            if (!result.IsSuccess || result.DecryptedData == null)
            {
                return Err("0", result.MessageString);
            }

            var data = result.DecryptedData;
            string responseClientRefId = data["ClientRefID"]?.ToString() ?? "";
            if (!string.IsNullOrEmpty(responseClientRefId)
                && !responseClientRefId.Equals(request.ClientRefID, StringComparison.OrdinalIgnoreCase))
                return Err("0", "Transaction enquiry reference mismatch");

            string finoStatus = data["TransactionStatus"]?.ToString() ?? "";
            string mappedStatus = MapTransactionStatus(finoStatus);
            string rrn = data["RRN"]?.ToString() ?? txn.Brid ?? "";

            if (mappedStatus is "SUCCESS" or "FAILED")
            {
                var reconciliation = await ReconcileTransactionAsync(txn, mappedStatus, ct);
                if (!reconciliation.Success)
                    return Err("0", reconciliation.Message);
                if (reconciliation.NewBalance.HasValue)
                    txn.NewBal = Convert.ToString(reconciliation.NewBalance.Value);
            }

            txn.Status = mappedStatus;
            txn.UpdateDate = DateTime.Now;
            txn.Brid = rrn;
            txn.ApiMsg = result.MessageString;
            txn.ApiRes = result.RawResponse.Length <= 4000 ? result.RawResponse : result.RawResponse[..4000];
            await _context.SaveChangesAsync(ct);

            return new FinoAepsResponse
            {
                Status_Code = "1",
                Message = result.MessageString,
                Data = new FinoAepsTransactionStatusData
                {
                    ClientRefID = request.ClientRefID,
                    TransactionStatus = mappedStatus,
                    Amount = data["Amount"]?.ToString(),
                    TransactionDateTime = data["TransactionDateTime"]?.ToString(),
                    RRN = rrn,
                    AdhaarNo = data["AdhaarNo"]?.ToString()
                }
            };
        }

        private async Task<(bool Success, string Message, decimal? NewBalance)> ReconcileTransactionAsync(
            TransactionDetail txn, string status, CancellationToken ct)
        {
            if (!int.TryParse(txn.UserId, out int userId))
                return (false, "Invalid transaction user", null);

            string txnType = txn.TxnType?.ToUpperInvariant() ?? "";
            if (status == "SUCCESS" && txnType == "CW")
            {
                decimal amount = IsOnUsTransaction(txn) ? txn.Amount ?? 0 : txn.Cost ?? 0;
                return await EnsureFinoCreditAsync(userId, amount, "CW", txn, IsOnUsTransaction(txn), ct);
            }
            if (status == "SUCCESS" && txnType == "AP")
                return await EnsureWalletCreditAsync(userId, txn.Amount ?? 0, 0, "AEPS AP", txn, ct);
            if (status == "FAILED" && txnType == "CD")
                return await EnsureWalletCreditAsync(userId, (txn.Amount ?? 0) + (txn.Charge ?? 0), txn.Charge ?? 0, "AEPS CD Refund", txn, ct);
            if (status == "FAILED" && txnType == "AP")
                return await EnsureWalletCreditAsync(userId, txn.Charge ?? 0, txn.Charge ?? 0, "AEPS AP Refund", txn, ct);

            return (true, "Success", status == "FAILED" ? txn.OldBal : null);
        }

        private async Task<(bool Success, string Message, decimal? NewBalance)> EnsureFinoCreditAsync(
            int userId, decimal amount, string txnType, TransactionDetail txn, bool isFinoBank, CancellationToken ct)
        {
            decimal? existingBalance = await FindWalletEntryBalanceAsync(userId, $"AEPS {txnType}", txn.TxnId ?? "", ct);
            if (existingBalance.HasValue)
                return (true, "Success", existingBalance);
            if (amount <= 0)
                return (false, "Invalid transaction amount", null);

            await _finoWalletService.CreditAsync(userId, amount, txnType, txn.BankName ?? "", txn.TxnId ?? "", isFinoBank, ct);
            decimal? newBalance = await FindWalletEntryBalanceAsync(userId, $"AEPS {txnType}", txn.TxnId ?? "", ct);
            return newBalance.HasValue
                ? (true, "Success", newBalance)
                : (false, "Wallet credit not verified. Transaction kept as PENDING", null);
        }

        private async Task<(bool Success, string Message, decimal? NewBalance)> EnsureWalletCreditAsync(
            int userId, decimal amount, decimal charge, string walletTxnType, TransactionDetail txn, CancellationToken ct)
        {
            decimal? existingBalance = await FindWalletEntryBalanceAsync(userId, walletTxnType, txn.TxnId ?? "", ct);
            if (existingBalance.HasValue)
                return (true, "Success", existingBalance);
            if (amount <= 0)
                return (false, "Invalid transaction amount", null);

            var (_, newBalance, entryId) = await _walletService.CreditAsync(
                userId, txn.UserName ?? "", txn.Amount ?? 0, amount, charge, 0,
                walletTxnType,
                $"{walletTxnType} | Bank: {txn.BankName} | TxnId: {txn.TxnId}",
                txn.WlId, ct);
            return entryId > 0
                ? (true, "Success", newBalance)
                : (false, "Wallet credit not verified. Transaction kept as PENDING", null);
        }

        private async Task<decimal?> FindWalletEntryBalanceAsync(int userId, string txnType, string txnId, CancellationToken ct)
        {
            return await _context.Tbluserbalances
                .Where(b => b.UserId == userId && b.TxnType == txnType && b.Remarks != null && b.Remarks.EndsWith($"TxnId: {txnId}"))
                .OrderByDescending(b => b.Id)
                .Select(b => b.NewBal)
                .FirstOrDefaultAsync(ct);
        }

        private static bool IsOnUsTransaction(TransactionDetail txn)
        {
            try { return JObject.Parse(txn.ApiReq ?? "{}")["NBIN"]?.ToString() == "608001"; }
            catch { return false; }
        }

        private static string MapTransactionStatus(string status)
        {
            string normalized = status.Trim().ToUpperInvariant();
            if (normalized is "0" or "00" || normalized.Contains("SUCCESS")) return "SUCCESS";
            if (normalized == "1" || normalized.Contains("FAIL")) return "FAILED";
            return "PENDING";
        }

        // ── SESSION / USER ────────────────────────────────────────────
        private async Task<string?> ValidateSessionAsync(string username, CancellationToken ct)
        {
            try
            {
                var user = await _context.TblUsers
                    .Where(u => u.Id == Convert.ToInt32(username) && u.Status == "Active")
                    .Select(u => new { u.Id })
                    .FirstOrDefaultAsync(ct);
                return user?.Id.ToString();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ValidateSession DB error");
                return null;
            }
        }

        private async Task<(string? Lat, string? Lng)> GetUserLatLongAsync(string username, string sessionKey, CancellationToken ct)
        {
            try
            {
                var user = await _context.TblUsers
                    .Where(u => u.Id == Convert.ToInt32(username) && u.SessionKey == sessionKey)
                    .Select(u => new { u.Lat, u.Longitute })
                    .FirstOrDefaultAsync(ct);
                return (user?.Lat, user?.Longitute);
            }
            catch { return (null, null); }
        }

        private static FinoAepsResponse Err(string code, string msg)
            => new() { Status_Code = code, Message = msg, Data = msg };

    }
}

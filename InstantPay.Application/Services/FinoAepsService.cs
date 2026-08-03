// ── FINO AEPS ORCHESTRATOR ───────────────────────────────────────────────────
// Validates the incoming request (APIKey + SessionKey), resolves lat/long,
// generates a TxnId, then delegates to the appropriate per-txnType service.
// All encryption, HTTP, DB, and wallet logic lives in the dedicated services.
// ─────────────────────────────────────────────────────────────────────────────
using InstantPay.Application.Interfaces;
using InstantPay.Application.Interfaces.FinoAeps;
using InstantPay.Infrastructure.Sql.Entities;
using InstantPay.SharedKernel.RequestPayload.FinoAEPS;
using InstantPay.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace InstantPay.Application.Services
{
    public class FinoAepsService : IFinoAepsService
    {
        private readonly AppDbContext _context;
        private readonly IFinoAepsApiClient _apiClient;
        private readonly IInstantPayLogService _logService;
        private readonly ILogger<FinoAepsService> _logger;
        private readonly string _validApiKey;

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
            _logger         = logger;
            _validApiKey    = config["FinoAEPS:ValidApiKey"] ?? "FinoAEPS001";
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
                string? userId = await ValidateSessionAsync(username, request.SessionKey!, ct);
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

        // ── SESSION / USER ────────────────────────────────────────────
        private async Task<string?> ValidateSessionAsync(string username, string sessionKey, CancellationToken ct)
        {
            try
            {
                var user = await _context.TblUsers
                    .Where(u => u.Id == Convert.ToInt32(username) && u.SessionKey == sessionKey && u.Status == "Active")
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

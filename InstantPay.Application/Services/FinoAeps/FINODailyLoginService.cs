using InstantPay.Application.Interfaces.FinoAeps;
using InstantPay.Infrastructure.Sql.Entities;
using InstantPay.SharedKernel.RequestPayload.FinoAEPS;
using InstantPay.SharedKernel.Results;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace InstantPay.Application.Services.FinoAeps
{
    public class FINODailyLoginService : IFINODailyLoginService
    {
        private readonly IFinoAepsApiClient _api;
        private readonly IFinoAepsTransactionService _txnService;
        private readonly AppDbContext _context;
        private readonly ILogger<FINODailyLoginService> _logger;

        private readonly string _dlUrl;

        public FINODailyLoginService(
            IFinoAepsApiClient api,
            IFinoAepsTransactionService txnService,
            AppDbContext context,
            IConfiguration config,
            ILogger<FINODailyLoginService> logger)
        {
            _api        = api;
            _txnService = txnService;
            _context    = context;
            _logger     = logger;

            _dlUrl = config["FinoAEPS:Prod:TwoFAAuthUrl"]
                     ?? "https://fpbs.fino.bank.in/AEPSAPIService/AEPSUIService.svc/ProcessRequest/TwoFAAuthentication";
        }

        public async Task<FinoAepsResponse> ProcessAsync(
            FinoAepsRequest request, string userId, string txnId,
            string lat, string lng, CancellationToken ct = default)
        {
            string bodyJson = JsonConvert.SerializeObject(new
            {
                MerchantID  = request.mobileno,
                Version     = "1001",
                ServiceID   = "167",
                ClientRefID = txnId,
                MobileNo    = request.mobileno,
                AadharNo    = request.aadharno,
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
                CheckSum    = _api.ComputeChecksum($"{txnId}+{request.mobileno}+{request.aadharno}"),
                IsIris      = request.deviceType
            });

            var pendingRec = new FinoAepsTxnRecord(
                UserId      : int.Parse(userId),
                TxnId       : txnId,
                Mobile      : request.mobileno,
                Amount      : 0m,
                Status      : "Pending",
                TxnType     : "DL",
                Rrn         : "",
                AadhaarNo   : request.aadharno,
                BankName    : request.BankName,
                Brid        : txnId,
                ApiMsg      : "",
                ApiRes      : "",
                ApiReq      : bodyJson,
                IfscCode    : null,
                CustomerName: null,
                OpId        : null,
                OperatorName: "FINO_AEPS_DAILY_LOGIN",
                ComingFrom: request.comingFrom
            );

            bool pendingInserted = await _txnService.InsertPendingAsync(pendingRec, ct);
            if (!pendingInserted)
                return Err("Failed to insert pending transaction");

            var result = await _api.PostProdAsync(_dlUrl, bodyJson, ct);

            string status = result.IsPending ? "PENDING" : result.IsSuccess ? "SUCCESS" : "FAILED";
            string rrn = result.DecryptedData?["RRN"]?.ToString() ?? "NA";
            await _txnService.UpdateStatusAsync(txnId, status, result.RawResponse, rrn, ct);

            if (!result.IsSuccess)
                return Err(result.MessageString);

            await SaveDailyLoginAsync(userId);

            return Ok(result.MessageString, new List<FinoOtherData>
            {
                new() { Status = "Success", TxnDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") }
            });
        }

        private async Task SaveDailyLoginAsync(string userId)
        {
            try
            {
                _context.AepsdailyLogins.Add(new AepsdailyLogin
                {
                    UserId    = userId,
                    LoginType = "FINO",
                    Logindate = DateTime.Now
                });
                await _context.SaveChangesAsync();
            }
            catch (Exception ex) { _logger.LogWarning(ex, "SaveDailyLogin failed for UserId={UserId}", userId); }
        }

        private static FinoAepsResponse Err(string msg) => new() { Status_Code = "0", Message = msg, Data = msg };
        private static FinoAepsResponse Ok(string msg, object data) => new() { Status_Code = "1", Message = msg, Data = data };
    }
}

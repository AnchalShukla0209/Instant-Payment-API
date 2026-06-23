using InstantPay.Application.Interfaces.FinoAeps;
using InstantPay.SharedKernel.RequestPayload.FinoAEPS;
using InstantPay.SharedKernel.Results;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace InstantPay.Application.Services.FinoAeps
{
    public class FINORegistrationService : IFINORegistrationService
    {
        private readonly IFinoAepsApiClient _api;
        private readonly IFinoAepsTransactionService _txnService;
        private readonly ILogger<FINORegistrationService> _logger;

        private readonly string _regUrl;
        private readonly string _latLongUrl;

        public FINORegistrationService(
            IFinoAepsApiClient api,
            IFinoAepsTransactionService txnService,
            IConfiguration config,
            ILogger<FINORegistrationService> logger)
        {
            _api        = api;
            _txnService = txnService;
            _logger     = logger;

            var prod    = config.GetSection("FinoAEPS:Prod");
            _regUrl     = prod["TwoFARegUrl"]   ?? "https://fpbs.fino.bank.in/AEPSAPIService/AEPSUIService.svc/ProcessRequest/TwoFARegistration";
            _latLongUrl = prod["LatLongRegUrl"] ?? "https://fpbs.fino.bank.in/AEPSAPIService/AEPSUIService.svc/ProcessRequest/AEPSLatLongUpdateAPI";
        }

        public async Task<FinoAepsResponse> ProcessAsync(
            FinoAepsRequest request, string userId, string txnId,
            string lat, string lng, CancellationToken ct = default)
        {
            string bodyJson = JsonConvert.SerializeObject(new
            {
                MerchantID  = request.mobileno,
                Version     = "1001",
                ServiceID   = "166",
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
                TxnType     : "REG",
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
                OperatorName: "FINO_AEPS_REGISTRATION",
                ComingFrom: request.comingFrom
            );

            bool pendingInserted = await _txnService.InsertPendingAsync(pendingRec, ct);
            if (!pendingInserted)
                return Err("Failed to insert pending transaction");

            var result = await _api.PostProdAsync(_regUrl, bodyJson, ct);

            string status = result.IsSuccess ? "SUCCESS" : "FAILED";
            string rrn = result.DecryptedData?["RRN"]?.ToString() ?? "NA";

            await _txnService.UpdateStatusAsync(txnId, status, result.RawResponse, rrn, ct);

            if (!result.IsSuccess)
                return Err(result.MessageString);

            await RegisterLatLongAsync(request.mobileno ?? "", lat, lng, ct);

            return Ok(result.MessageString, new List<FinoOtherData>
            {
                new() { Status = "Success", TxnDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") }
            });
        }

        private async Task RegisterLatLongAsync(string merchantId, string lat, string lng, CancellationToken ct)
        {
            try
            {
                string crid     = "LL" + DateTime.Now.ToString("yyyyMMddHHmmssfff");
                string bodyJson = JsonConvert.SerializeObject(new
                {
                    MerchantID  = merchantId,
                    Version     = "1001",
                    ServiceID   = "223",
                    ClientRefID = crid,
                    MobileNo    = merchantId,
                    Latitude    = lat,
                    Longitude   = lng
                });

                await _api.PostProdAsync(_latLongUrl, bodyJson, ct);
            }
            catch (Exception ex) { _logger.LogWarning(ex, "RegisterLatLong fire-and-forget failed"); }
        }

        private static FinoAepsResponse Err(string msg) => new() { Status_Code = "0", Message = msg, Data = msg };
        private static FinoAepsResponse Ok(string msg, object data) => new() { Status_Code = "1", Message = msg, Data = data };
    }
}

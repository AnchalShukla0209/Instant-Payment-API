using InstantPay.Application.Interfaces.FinoAeps;
using InstantPay.SharedKernel.RequestPayload.FinoAEPS;
using InstantPay.SharedKernel.Results;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace InstantPay.Application.Services.FinoAeps
{
    public class FINOMerchantAuthService : IFINOMerchantAuthService
    {
        private readonly IFinoAepsApiClient _api;
        private readonly ILogger<FINOMerchantAuthService> _logger;

        private readonly string _maUrl;

        public FINOMerchantAuthService(
            IFinoAepsApiClient api,
            IConfiguration config,
            ILogger<FINOMerchantAuthService> logger)
        {
            _api    = api;
            _logger = logger;

            var prod = config.GetSection("FinoAEPS:Prod");
            _maUrl   = prod["MerchantAuthUrl"]
                ?? "https://fpbs.fino.bank.in/AEPSAPIService/AEPSUIService.svc/ProcessRequest/AEPSMerchantAuth";
        }

        public async Task<FinoAepsResponse> ProcessAsync(
            FinoAepsRequest request, string userId, string txnId,
            string lat, string lng, CancellationToken ct = default)
        {
            string bodyJson = JsonConvert.SerializeObject(new
            {
                MerchantID  = request.mobileno,
                ServiceID   = "187",
                Version     = "1001",
                ClientRefID = txnId,
                MobileNo    = request.mobileno,
                PidData     = request.fingerdata,
                RC          = "Y",
                TerminalId  = request.mobileno,
                IPAddress   = _api.ProdIPAddress,
                Latitude    = lat,
                Longitude   = lng,
                IMEI_MAC    = _api.GetMacAddress(),
                DeviceNo    = request.DeviceSrNo,
                CheckSum    = _api.ComputeChecksum($"{txnId}+{request.mobileno}"),
                IsIris      = request.deviceType
            });

            var result = await _api.PostProdAsync(_maUrl, bodyJson, ct);

            if (!result.IsSuccess)
                return Err(result.MessageString);

            string maTxnId = result.DecryptedData?["TransactionId"]?.ToString() ?? "";
            if (string.IsNullOrWhiteSpace(maTxnId))
                return Err("Merchant auth did not return a transaction id");

            return Ok(result.MessageString, new
            {
                TransactionId = maTxnId
            });
        }

        private static FinoAepsResponse Err(string msg) => new() { Status_Code = "0", Message = msg, Data = msg };
        private static FinoAepsResponse Ok(string msg, object data) => new() { Status_Code = "1", Message = msg, Data = data };
    }
}

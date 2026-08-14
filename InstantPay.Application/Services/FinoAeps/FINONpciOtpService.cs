using InstantPay.Application.Interfaces.FinoAeps;
using InstantPay.SharedKernel.RequestPayload.FinoAEPS;
using InstantPay.SharedKernel.Results;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace InstantPay.Application.Services.FinoAeps
{
    public class FINONpciOtpService : IFINONpciOtpService
    {
        private readonly IFinoAepsApiClient _api;
        private readonly ILogger<FINONpciOtpService> _logger;

        private readonly string _npciOtpUrl;

        public FINONpciOtpService(
            IFinoAepsApiClient api,
            IConfiguration config,
            ILogger<FINONpciOtpService> logger)
        {
            _api        = api;
            _logger     = logger;

            var prod    = config.GetSection("FinoAEPS:Prod");
            _npciOtpUrl = prod["NpciOTPSendUrl"]
                ?? "https://fpbs.fino.bank.in/AEPSAPIService/AEPSUIService.svc/ProcessRequest/AEPSNpciOtpAPI";
        }

        public async Task<FinoAepsResponse> ProcessAsync(
            FinoAepsRequest request, string userId, string txnId,
            string lat, string lng, CancellationToken ct = default)
        {
            string tranType = (request.npciOtpFor ?? "CASHWAEPSACQ").Trim();
            if (tranType != "CASHWAEPSACQ" && tranType != "CASHWAPAYACQ")
                return Err("npciOtpFor must be CASHWAEPSACQ or CASHWAPAYACQ");

            string bodyJson = JsonConvert.SerializeObject(new
            {
                ClientRefID    = txnId,
                MobileNo       = request.customermobileno ?? request.mobileno,
                NBIN           = request.bankiinno ?? "876880",
                MerchantID     = request.mobileno,
                ServiceID      = "227",
                AadharNo       = request.aadharno,
                Trantype       = tranType,
                Version        = "1001",
                Latitude       = lat,
                Longitude      = lng
            });

            var result = await _api.PostProdAsync(_npciOtpUrl, bodyJson, ct);

            if (!result.IsSuccess)
                return Err(result.MessageString);

            string npciTxnId  = result.DecryptedData?["TransactionId"]?.ToString() ?? "";
            string npciRefNo  = result.DecryptedData?["txnReferenceNo"]?.ToString() ?? "";
            string uidaiDataTxn = result.DecryptedData?["UidaiDataTxn"]?.ToString() ?? "";

            if (string.IsNullOrWhiteSpace(npciTxnId) || string.IsNullOrWhiteSpace(npciRefNo))
                return Err("NPCI OTP response did not return TransactionId / TxnReferenceNo");

            return Ok(result.MessageString, new
            {
                TransactionId  = npciTxnId,
                txnReferenceNo = npciRefNo,
                UidaiDataTxn   = uidaiDataTxn
            });
        }

        private static FinoAepsResponse Err(string msg) => new() { Status_Code = "0", Message = msg, Data = msg };
        private static FinoAepsResponse Ok(string msg, object data) => new() { Status_Code = "1", Message = msg, Data = data };
    }
}

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
            string tranType = (request.npciOtpFor ?? "CASHWAEPS").Trim().ToUpperInvariant();
            if (tranType != "CASHWAEPS" && tranType != "ADHARPAY")
                return Err("npciOtpFor must be CASHWAEPS or AdharPay");

            // Display label per doc
            string tranTypeValue = tranType == "ADHARPAY" ? "AdharPay" : "CASHWAEPS";

            string bodyJson = JsonConvert.SerializeObject(new
            {
                ClientRefID    = txnId,
                MobileNo       = request.customermobileno ?? request.mobileno,
                NBIN           = "100012",
                MerchantID     = request.mobileno,
                ServiceID      = "227",
                AdhaarNo       = request.aadharno,
                Trantype       = tranTypeValue,
                Version        = "1001"
            });

            var result = await _api.PostProdAsync(_npciOtpUrl, bodyJson, ct);

            if (!result.IsSuccess)
                return Err(result.MessageString);

            string npciTxnId  = result.DecryptedData?["TransactionId"]?.ToString() ?? "";
            string npciRefNo  = result.DecryptedData?["TxnReferenceNo"]?.ToString() ?? "";

            if (string.IsNullOrWhiteSpace(npciTxnId) || string.IsNullOrWhiteSpace(npciRefNo))
                return Err("NPCI OTP response did not return TransactionId / TxnReferenceNo");

            return Ok(result.MessageString, new
            {
                TransactionId  = npciTxnId,
                TxnReferenceNo = npciRefNo
            });
        }

        private static FinoAepsResponse Err(string msg) => new() { Status_Code = "0", Message = msg, Data = msg };
        private static FinoAepsResponse Ok(string msg, object data) => new() { Status_Code = "1", Message = msg, Data = data };
    }
}

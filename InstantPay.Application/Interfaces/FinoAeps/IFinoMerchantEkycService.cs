using InstantPay.SharedKernel.RequestPayload.FinoAEPS;
using Newtonsoft.Json;

namespace InstantPay.Application.Interfaces.FinoAeps
{
    public interface IFinoMerchantEkycService
    {
        Task<FinoMerchantEkycResponse> ProcessAsync(FinoMerchantEkycRequest request, CancellationToken ct = default);
    }

    public class FinoMerchantEkycResponse
    {
        [JsonProperty("Status_Code")]
        public string Status_Code { get; set; } = "0";

        [JsonProperty("Message")]
        public string Message { get; set; } = string.Empty;

        [JsonProperty("Data")]
        public string Data { get; set; } = string.Empty;
    }
}

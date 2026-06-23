using InstantPay.SharedKernel.RequestPayload.FinoAEPS;
using Newtonsoft.Json;

namespace InstantPay.Application.Interfaces.FinoAeps
{
    public interface IFinoAepsDailyLoginCheckService
    {
        Task<FinoAepsDailyLoginCheckResponse> CheckDailyLoginAsync(FinoAepsDailyLoginCheckRequest request, CancellationToken ct = default);
    }

    public class FinoAepsDailyLoginCheckResponse
    {
        [JsonProperty("Status_Code")]
        public string Status_Code { get; set; } = "0";

        [JsonProperty("Message")]
        public string Message { get; set; } = string.Empty;

        [JsonProperty("Data")]
        public string Data { get; set; } = string.Empty;
    }
}

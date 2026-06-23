using Newtonsoft.Json.Linq;

namespace InstantPay.Application.Interfaces.FinoAeps
{
    public class FinoApiCallResult
    {
        public bool IsSuccess { get; set; }
        public string MessageString { get; set; } = "";
        public JObject? DecryptedData { get; set; }
        public string RawResponse { get; set; } = "";
    }
}

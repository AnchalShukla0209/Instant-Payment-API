using Newtonsoft.Json.Linq;

namespace InstantPay.Application.Interfaces.FinoAeps
{
    public class FinoApiCallResult
    {
        public bool IsSuccess { get; set; }
        public bool IsPending { get; set; }
        public string ResponseCode { get; set; } = "-1";
        public string MessageString { get; set; } = "";
        public JObject? DecryptedData { get; set; }
        public string RawResponse { get; set; } = "";
    }
}

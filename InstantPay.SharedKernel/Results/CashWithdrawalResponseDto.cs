using InstantPay.SharedKernel.Results.CashWithdrawal;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InstantPay.SharedKernel.Results
{
    public class CashWithdrawalResponseDto
    {
        [JsonProperty("responseCode")]
        public string ResponseCode { get; set; }
        public bool Success { get; set; }

        [JsonProperty("responseMessage")]
        public string ResponseMessage { get; set; }

        [JsonProperty("responseData")]
        public CashWithdrawalResponseData ResponseData { get; set; }

        [JsonProperty("traceid")]
        public string TraceId { get; set; }
        public string accessToken { get; set; }
        public string appIdentifierToken { get; set; }
    }
}

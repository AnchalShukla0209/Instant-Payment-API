using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InstantPay.SharedKernel.Results
{
    public class AgentEKYCResponseDto
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public string StatusCode { get; set; }
        public string RawResponse { get; set; }
        public string ExternalAppRef { get; set; }
        public string ApplicationNumber { get; set; }
        public string Status { get; set; }
        public string ErrorCode { get; set; }
        public string ErrorMessage { get; set; }
        public string accessToken { get; set; }
        public string appIdentifierToken { get; set; }
    }
}

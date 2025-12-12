using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InstantPay.SharedKernel.RequestPayload
{
    public class MiniStatementRequest
    {
        public string AgentLoginId { get; set; }
        public string AgentPin { get; set; }
        public string Aadhaar { get; set; }
        public string BankId { get; set; }
        public string BankName { get; set; }
        public string Mobile { get; set; }
        public string FingerprintXml { get; set; }
        public string Latitude { get; set; }
        public string Longitude { get; set; }
        public string AccessToken { get; set; }
        public string ComingFrom { get; set; } = "WEB";
        public string AppIdentifierToken { get; set; }
        public string UserId { get; set; }
    }
}

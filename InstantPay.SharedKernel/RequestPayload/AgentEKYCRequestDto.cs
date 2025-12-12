using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InstantPay.SharedKernel.RequestPayload
{
    public class AgentEKYCRequestDto
    {
        public string ApplicationNumber { get; set; }
        public string AadhaarValue { get; set; } 
        public string PidXml { get; set; } 
        public string AccessToken { get; set; }
        public string AppIdentifierToken { get; set; }
        public string Mobile { get; set; }
        public string InitiatingEntityId { get; set; } = "7215";
        public string ApiVersion { get; set; } = "1.0";
        public string ActionType { get; set; } = "Creation";
        public string ActionSubType { get; set; } = "Save";
        public string ConsentText { get; set; } = "I hereby provide my consent for Aadhaar eKYC authentication.";
        public string ConsentCode { get; set; } = "CONSENT";
        public string ConsentVersion { get; set; } = "1.0";
    }
}

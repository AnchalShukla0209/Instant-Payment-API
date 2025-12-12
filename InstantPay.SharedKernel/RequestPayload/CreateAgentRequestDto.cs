using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InstantPay.SharedKernel.RequestPayload
{
    public class CreateAgentRequestDto
    {
        public string RefNo { get; set; } = string.Empty;
        public string PAN { get; set; } = string.Empty;
        public string Mobile { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public string State { get; set; } = string.Empty;
        public string Pincode { get; set; } = string.Empty;
        public int? UserId { get; set; }
        public string UserType { get; set; } = "AGENT";
        public string AccessToken { get; set; }
        public string AppIdentifierToken { get; set; }
    }
}

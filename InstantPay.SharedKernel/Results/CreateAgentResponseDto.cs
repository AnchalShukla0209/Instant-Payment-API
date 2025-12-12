using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InstantPay.SharedKernel.Results
{
    public class CreateAgentResponseDto
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string ApplicationNumber { get; set; } = string.Empty;
        public string AgentRefNo { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string StatusCode { get; set; } = string.Empty; 
        public string AccessToken { get; set; } = string.Empty; 
        public string AppIdentifierToken { get; set; } = string.Empty; 
    }
}

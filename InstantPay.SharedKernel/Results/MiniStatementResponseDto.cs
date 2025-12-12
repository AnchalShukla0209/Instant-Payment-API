using InstantPay.SharedKernel.Results.MiniStatement;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InstantPay.SharedKernel.Results
{
    public class MiniStatementResponseDto
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public string StatusCode { get; set; }
        public string RawResponse { get; set; }
        public MiniStatementResponse ParsedResponse { get; set; } // strongly-typed parsed response
        public int? TransId { get; set; } // local DB id
        public string accessToken { get; set; }
        public string appIdentifierToken { get; set; }
    }
}

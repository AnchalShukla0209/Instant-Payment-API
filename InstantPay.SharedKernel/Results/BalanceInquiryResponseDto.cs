using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InstantPay.SharedKernel.Results
{
    public class BalanceInquiryResponseDto
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public string StatusCode { get; set; }
        public string RawResponse { get; set; }
        public string ApiTxnId { get; set; }
        public string Rrn { get; set; }
        public string TransactionId { get; set; }
        public decimal? Balance { get; set; }
        public string AccountExists { get; set; }
        public int? TransId { get; set; }
        public string accessToken { get; set; }
        public string appIdentifierToken { get; set; }
    }
}

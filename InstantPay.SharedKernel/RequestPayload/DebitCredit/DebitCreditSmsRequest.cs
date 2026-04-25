using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InstantPay.SharedKernel.RequestPayload.DebitCredit
{
    public class DebitCreditSmsRequest
    {
        public string? TransferType { get; set; }
        public string? ReceiverPhone { get; set; }
        public decimal? ReceiverPreAmount { get; set; }
        public decimal? ReceiverCurrentAmount { get; set; }
        public string? ReceiverName { get; set; }
        public decimal? TransactionAmount { get; set; }
    }
}

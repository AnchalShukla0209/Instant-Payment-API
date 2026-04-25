using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InstantPay.SharedKernel.RequestPayload.MoneyTransfer.Castler
{
    public class CastlerCallback
    {
        public string? CustomerRefId { get; set; }
        public string? Status { get; set; }
        public string? Utr { get; set; }
        public string? Remarks { get; set; }
    }
}

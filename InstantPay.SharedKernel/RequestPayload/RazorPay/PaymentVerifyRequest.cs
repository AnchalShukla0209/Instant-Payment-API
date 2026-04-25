using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InstantPay.SharedKernel.RequestPayload.RazorPay
{
    public class PaymentVerifyRequest
    {
        public string PaymentId { get; set; }
        public string OrderId { get; set; }
        public string Signature { get; set; }
    }
}

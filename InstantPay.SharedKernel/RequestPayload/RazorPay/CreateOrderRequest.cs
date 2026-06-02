using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InstantPay.SharedKernel.RequestPayload.RazorPay
{
    public class CreateOrderRequest
    {
        public decimal Amount { get; set; }
        public string Mobile { get; set; }
        public string Pan { get; set; }
        public string Aadhar { get; set; }
        public string Name { get; set; }
        public int UserId { get; set; }
        public string? comingfrom { get; set; }
        public string? PaymentMethod { get; set; }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InstantPay.SharedKernel.Results.RazorPay
{
    public class CreateOrderResponse
    {
        public string OrderId { get; set; }
        public decimal Amount { get; set; }
        public string Key { get; set; }
        public bool success { get; set; }
    }
}

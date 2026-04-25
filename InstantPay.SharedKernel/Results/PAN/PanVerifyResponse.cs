using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InstantPay.SharedKernel.Results.PAN
{
    public class PanVerifyResponse
    {
        public bool Success { get; set; }
        public string? Name { get; set; }
        public string? Message { get; set; }
    }
}

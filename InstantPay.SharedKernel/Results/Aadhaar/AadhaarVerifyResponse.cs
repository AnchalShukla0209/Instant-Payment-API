using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InstantPay.SharedKernel.Results.Aadhaar
{
    public class AadhaarVerifyResponse
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public string? State { get; set; }
        public string? AgeRange { get; set; }
        public string? Gender { get; set; }
        public string? MaskedMobileNumber { get; set; }
    }
}

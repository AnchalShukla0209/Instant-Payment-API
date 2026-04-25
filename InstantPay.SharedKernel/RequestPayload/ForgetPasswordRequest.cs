using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InstantPay.SharedKernel.RequestPayload
{
    public class ForgetPasswordRequest
    {
        public string Mobile { get; set; }
        public string AadharNumber { get; set; }
        public string PANNumber { get; set; }
    }

    public class ResetPasswordRequest
    {
        public string Token { get; set; }
        public string Otp { get; set; }
        public string NewPassword { get; set; }
    }

    public class ResendOtpRequest
    {
        public string Token { get; set; }
    }

}

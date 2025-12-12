using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InstantPay.Application.DTOs
{
    public class LoginResponseDto
    {
        public string Token { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string Usertype { get; set; } = string.Empty;
        public string? OTP { get; set; }
        public bool? IsOtpRequired { get; set; }
        public string? messaege { get; set; }
        public string? Phoneno { get; set; }
    }

    public class OTPSuccessResponse
    {
        public string message { get; set; } = string.Empty;
        public bool? success { get; set; }
    }
}

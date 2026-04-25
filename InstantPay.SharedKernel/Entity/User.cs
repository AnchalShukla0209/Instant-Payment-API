using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InstantPay.SharedKernel.Entity
{
    public class User
    {
        public int Id { get; set; }
        public string? Username { get; set; }
        public string? Password { get; set; }
        public string? Status { get; set; }
        public string? Usertype { get; set; }
        public string? OTP { get; set; }
        public bool? IsOtpRequired { get; set; }
        public string? Phoneno { get; set; }
    }

    public class OtpLoginLogDto
    {
        public string? usertype { get; set; }
        public string? userid { get; set; }
    }

    public class Superadmindashboardpayload
    {
        public int? ServiceId { get; set; }
        public int? Year { get; set; }
        
    }

    
    public record UnlockRequestDto
    {
        public string? UserId { get; init; } = "";
        public string? Method { get; init; } = ""; 
        public string? Value { get; init; } = ""; 
        public string? UserType { get; init; } = ""; 
    }

    public record UserRequestForCP
    {
        public string? UserId { get; init; } = "";
        public string? TxnPin { get; init; } = "";
        public string? PANNo { get; init; } = "";
        public string? AadharNo { get; init; } = "";
        public string? OTP { get; init; } = "";
        public string? MPin { get; init; } = "";
        public string? OldPassword { get; init; } = "";
        public string? NewPassword { get; init; } = "";
        public string? ConfirmPassword { get; init; } = "";
        public string? Mode { get; init; } = "";
    }

    public record UnlockResponseDto
    {
        public string Token { get; init; } = "";
        public string Username { get; init; } = "";
        public string Usertype { get; init; } = "";
        public string message { get; init; } = "";
        public string OTP { get; init; } = "";
        public bool IsOtpRequired { get; init; } = false;
        public string Phoneno { get; init; } = "";
    }


}

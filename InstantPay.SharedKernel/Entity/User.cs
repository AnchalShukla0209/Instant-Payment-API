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

}

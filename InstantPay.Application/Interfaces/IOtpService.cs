using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InstantPay.Application.Interfaces
{
    public interface IOtpService
    {
        Task<string> SendOtpAsync(string mobile, string otp);
        Task<string> GetMacAddress();
    }

}

using InstantPay.SharedKernel.Results.Aadhaar;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InstantPay.Application.Interfaces.Aadhaar
{
    public interface IAadhaarService
    {
        Task<AadhaarVerifyResponse> VerifyAadhaarAsync(string aadhaarNumber);
    }
}

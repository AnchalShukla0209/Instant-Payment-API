using InstantPay.Application.DTOs;
using InstantPay.SharedKernel.Entity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InstantPay.Application.Interfaces
{
    public interface ILoginService
    {
        Task<LoginResponseDto?> LoginAsync(LoginRequestDto request);

        Task<OTPSuccessResponse?> VerifyOTP(OtpLoginLogDto request);

        Task<LoginResponseDto?> ResendOTP(OtpLoginLogDto request);
    }
}

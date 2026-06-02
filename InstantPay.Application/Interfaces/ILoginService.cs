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
        Task<LoginResponseDto?> LoginAsync(LoginRequestDto request, string platform);

        Task<LoginResponseDto?> VerifyOTP(OtpLoginLogDto request, string platform);

        Task<LoginResponseDto?> ResendOTP(OtpLoginLogDto request);

        Task<ServiceRightsData> GetUserRightsInfoDet(int Id);
        Task<bool> LogoutAsync(int userId, string platform);
    }
}

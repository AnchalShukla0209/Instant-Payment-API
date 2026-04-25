using InstantPay.SharedKernel.Entity;
using InstantPay.SharedKernel.RequestPayload;
using InstantPay.SharedKernel.Results;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InstantPay.Application.Interfaces
{
    public interface IAuthService
    {
        Task<UnlockResponseDto?> UnlockAsync(UnlockRequestDto request);
        string GenerateJwtToken(User user);
        Task<ResponseSuccess> UpdateUserInfo(UserRequestForCP request);

        Task<ResponseSuccess> ForgetPassword(ForgetPasswordRequest request);

        Task<ResponseSuccess> ResetPassword(ResetPasswordRequest request);

        Task<ResponseSuccess> ResendResetOtp(ResendOtpRequest request);

        Task<ResponseSuccess> ValidateUserInfoAndSentOTP(UserRequestForCP request);

        Task<ResponseSuccess> ExpirtCheckForForgetPassword(ResetPasswordRequest request);
    }

}

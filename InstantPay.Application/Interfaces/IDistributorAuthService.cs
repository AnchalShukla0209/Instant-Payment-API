using InstantPay.Application.DTOs;

namespace InstantPay.Application.Interfaces;

public interface IDistributorAuthService
{
    Task<DistributorAuthResult<DistributorLoginChallengeResponse>> LoginAsync(
        DistributorLoginRequest request,
        string expectedUserType,
        string ipAddress,
        CancellationToken cancellationToken);

    Task<DistributorAuthResult<DistributorTokenResponse>> VerifyOtpAsync(
        DistributorOtpRequest request,
        string expectedUserType,
        string ipAddress,
        CancellationToken cancellationToken);
}

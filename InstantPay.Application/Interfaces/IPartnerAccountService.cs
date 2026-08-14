using InstantPay.Application.DTOs;
using InstantPay.SharedKernel.Results;

namespace InstantPay.Application.Interfaces;

public interface IPartnerAccountService
{
    Task<PartnerAccountProfileDto?> GetProfileAsync(int partnerId, string userType, CancellationToken cancellationToken);

    Task<ResponseSuccess> ValidateAndSendOtpAsync(
        int partnerId,
        string userType,
        PartnerValidateAccountRequest request,
        CancellationToken cancellationToken);

    Task<ResponseSuccess> ResendOtpAsync(int partnerId, CancellationToken cancellationToken);

    Task<ResponseSuccess> ChangePasswordAsync(
        int partnerId,
        PartnerChangePasswordRequest request,
        CancellationToken cancellationToken);

    Task<ResponseSuccess> ChangeMpinAsync(
        int partnerId,
        PartnerChangeMpinRequest request,
        CancellationToken cancellationToken);

    Task<ResponseSuccess> ChangeTxnPinAsync(
        int partnerId,
        PartnerChangeTxnPinRequest request,
        CancellationToken cancellationToken);
}

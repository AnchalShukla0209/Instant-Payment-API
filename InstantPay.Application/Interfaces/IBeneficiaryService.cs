using InstantPay.Application.DTOs;

namespace InstantPay.Application.Interfaces;

public interface IBeneficiaryService
{
    Task<SaveBeneficiaryResponse> SaveBeneficiaryAsync(SaveBeneficiaryRequest request);
    Task<SendOtpResponse> SendOtpAsync(SendOtpRequest request);
    Task<SendOtpResponse> ResendOtpAsync(SendOtpRequest request);
    Task<DeleteBeneficiaryResponse> DeleteBeneficiaryAsync(DeleteBeneficiaryRequest request);
    Task<GetBeneficiaryListResponse> GetBeneficiaryListAsync(GetBeneficiaryListRequest request);
}

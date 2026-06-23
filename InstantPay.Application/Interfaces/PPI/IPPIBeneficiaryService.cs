using InstantPay.Application.DTOs;

namespace InstantPay.Application.Interfaces.PPI;

public interface IPPIBeneficiaryService
{
    Task<PPIBeneListResponse> GetBeneficiaryListAsync(PPIBeneListRequest request);
    Task<PPIAddBeneResponse> AddBeneficiaryAsync(PPIAddBeneRequest request);
    Task<PPIResendOtpResponse> ResendOtpAsync(PPIResendOtpRequest request);
    Task<PPIValidateOtpResponse> ValidateOtpAsync(PPIValidateOtpRequest request);
    Task<PPIDeleteOtpResponse> DeleteGetOtpAsync(PPIDeleteOtpRequest request);
    Task<PPIDeleteVerifyResponse> DeleteVerifyOtpAsync(PPIDeleteVerifyRequest request);
}

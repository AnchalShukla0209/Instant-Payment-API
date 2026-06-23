using InstantPay.Application.DTOs;

namespace InstantPay.Application.Interfaces.PPI;

public interface IPPIAadharService
{
    Task<PPIAadharOtpResponse> GenerateAadharOtpAsync(PPIAadharOtpRequest request);
    Task<PPIValidateAadharOtpResponse> ValidateAadharOtpAsync(PPIValidateAadharOtpRequest request);
    Task<PPIAadharBiometricResponse> AadharBiometricAsync(PPIAadharBiometricRequest request);
    Task<PPIPanResponse> ValidatePanAsync(PPIPanRequest request);
}

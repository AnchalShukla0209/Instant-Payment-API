using InstantPay.Application.DTOs;

namespace InstantPay.Application.Interfaces.PPI;

public interface IPPIOtpService
{
    Task<GeneratePPIOtpResponse> GenerateOtpAsync(GeneratePPIOtpRequest request);
    Task<VerifyPPIOtpResponse> VerifyOtpAsync(VerifyPPIOtpRequest request);
}

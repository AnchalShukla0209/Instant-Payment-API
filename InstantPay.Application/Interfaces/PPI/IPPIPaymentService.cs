using InstantPay.Application.DTOs;

namespace InstantPay.Application.Interfaces.PPI;

public interface IPPIPaymentService
{
    Task<PPISendPaymentOtpResponse> SendPaymentOtpAsync(PPISendPaymentOtpRequest request);
}

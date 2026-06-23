using InstantPay.Application.DTOs;

namespace InstantPay.Application.Interfaces.PPI;

public interface IPPIFundTransferService
{
    Task<PPIFundTransferOtpResponse> GetOtpAsync(PPIFundTransferOtpRequest request);
}

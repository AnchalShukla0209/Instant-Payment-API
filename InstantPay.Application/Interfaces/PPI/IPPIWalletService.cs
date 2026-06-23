using InstantPay.Application.DTOs;

namespace InstantPay.Application.Interfaces.PPI;

public interface IPPIWalletService
{
    Task<PPILoadWalletResponse> LoadWalletAsync(PPILoadWalletRequest request);
}

using InstantPay.Application.DTOs;

namespace InstantPay.Application.Interfaces
{
    public interface ISettlementService
    {
        Task<SettlementDto> GetSettlementAsync(string? userId = null);
        Task<WithdrawalResponseDto> WithdrawAmountAsync(WithdrawalRequestDto request);
    }
}

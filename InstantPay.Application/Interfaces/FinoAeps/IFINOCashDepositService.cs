using InstantPay.SharedKernel.RequestPayload.FinoAEPS;
using InstantPay.SharedKernel.Results;

namespace InstantPay.Application.Interfaces.FinoAeps
{
    public interface IFINOCashDepositService
    {
        Task<FinoAepsResponse> ProcessAsync(FinoAepsRequest request, string userId, string txnId, string lat, string lng, CancellationToken ct = default);
    }
}

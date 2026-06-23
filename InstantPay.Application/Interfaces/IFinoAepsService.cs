using InstantPay.SharedKernel.RequestPayload.FinoAEPS;
using InstantPay.SharedKernel.Results;

namespace InstantPay.Application.Interfaces
{
    public interface IFinoAepsService
    {
        Task<FinoAepsResponse> ProcessAsync(FinoAepsRequest request, CancellationToken ct = default);
    }
}

using InstantPay.SharedKernel.RequestPayload;
using InstantPay.SharedKernel.Results.MoneyTransfer.Castler;

namespace InstantPay.Application.Interfaces
{
    public interface IAccountVerifyService
    {
        Task<LoginModel> VerifyAccountAsync(AccountVerifyRequest request, CancellationToken cancellationToken = default);
    }
}

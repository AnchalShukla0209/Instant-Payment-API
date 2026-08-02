using InstantPay.SharedKernel.RequestPayload.MoneyTransfer.AeronPay;
using InstantPay.SharedKernel.Results.MoneyTransfer.Castler;

namespace InstantPay.Application.Interfaces.MoneyTransfer.AeronPay
{
    public interface IAeronpayDmtService
    {
        Task<LoginModel> MoneyTransfer(AeronpayDmtRequest model, string ip, CancellationToken cancellationToken);
        Task<LoginModel> CheckStatus(string txnId, CancellationToken cancellationToken);
    }
}

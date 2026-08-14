using InstantPay.SharedKernel.RequestPayload.MoneyTransfer.AeronPay;
using InstantPay.SharedKernel.Results.MoneyTransfer.Castler;

namespace InstantPay.Application.Interfaces.MoneyTransfer.Tramo
{
    public interface ITramoUpiDmtService
    {
        Task<LoginModel> MoneyTransfer(AeronpayDmtRequest model, string ip, CancellationToken cancellationToken);
        Task<LoginModel> CheckStatus(string txnId, CancellationToken cancellationToken);
    }
}

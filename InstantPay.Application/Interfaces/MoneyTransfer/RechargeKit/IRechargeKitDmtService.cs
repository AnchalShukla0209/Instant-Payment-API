using InstantPay.SharedKernel.RequestPayload.MoneyTransfer.AeronPay;
using InstantPay.SharedKernel.RequestPayload.MoneyTransfer.RechargeKit;
using InstantPay.SharedKernel.Results.MoneyTransfer.Castler;
using InstantPay.SharedKernel.Results.MoneyTransfer.RechargeKit;

namespace InstantPay.Application.Interfaces.MoneyTransfer.RechargeKit
{
    public interface IRechargeKitDmtService
    {
        Task<LoginModel> MoneyTransfer(AeronpayDmtRequest model, string ip, CancellationToken cancellationToken);
        Task<LoginModel> CheckStatus(string txnId, CancellationToken cancellationToken);
        Task<RechargeKitOperatorResponse> GetCreditCardOperatorsAsync(CancellationToken cancellationToken = default);
        Task<LoginModel> CreditCardBillPaymentAsync(CreditCardBillPaymentRequest model, string ip, CancellationToken cancellationToken);
    }
}

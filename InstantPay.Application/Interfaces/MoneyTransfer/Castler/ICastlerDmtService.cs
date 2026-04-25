using InstantPay.SharedKernel.RequestPayload.MoneyTransfer.Castler;
using InstantPay.SharedKernel.Results.MoneyTransfer.Castler;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InstantPay.Application.Interfaces.MoneyTransfer.Castler
{
    public interface ICastlerDmtService
    {
        Task<LoginModel> MoneyTransfer(DmtRequest model, string ip, CancellationToken cancellationToken);
        Task<LoginModel> CheckStatus(string txnId, CancellationToken cancellationToken);
    }
}

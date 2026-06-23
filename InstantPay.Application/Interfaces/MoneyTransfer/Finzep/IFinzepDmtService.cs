using InstantPay.SharedKernel.RequestPayload.MoneyTransfer.Finzep;
using InstantPay.SharedKernel.Results.MoneyTransfer.Castler;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InstantPay.Application.Interfaces.MoneyTransfer.Finzep
{
    public interface IFinzepDmtService
    {
        Task<LoginModel> MoneyTransfer(FinzepDmtRequest model, string ip, CancellationToken cancellationToken);
        Task<LoginModel> CheckStatus(string txnId, CancellationToken cancellationToken);
    }
}

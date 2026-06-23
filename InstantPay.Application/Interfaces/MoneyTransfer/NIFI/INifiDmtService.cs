using InstantPay.SharedKernel.RequestPayload.MoneyTransfer.NIFI;
using InstantPay.SharedKernel.Results;
using InstantPay.SharedKernel.Results.MoneyTransfer.Castler;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InstantPay.Application.Interfaces.MoneyTransfer.NIFI
{
    public interface INifiDmtService
    {
        Task<LoginModel> MoneyTransfer(NifiDmtRequest model, string ip, CancellationToken cancellationToken);
        Task<LoginModel> CheckStatus(string txnId, CancellationToken cancellationToken);
    }
}

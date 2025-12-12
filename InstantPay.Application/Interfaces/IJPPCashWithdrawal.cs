using InstantPay.SharedKernel.RequestPayload;
using InstantPay.SharedKernel.Results;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InstantPay.Application.Interfaces
{
    public interface IJPPCashWithdrawal
    {
        Task<CashWithdrawalResponseDto> CashWithdrawalAsync(CashWithdrawalRequest model, CancellationToken cancellationToken = default);
    }
}

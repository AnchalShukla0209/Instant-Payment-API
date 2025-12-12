using InstantPay.SharedKernel.RequestPayload;
using InstantPay.SharedKernel.Results;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InstantPay.Application.Interfaces
{
    public interface IJPBMiniStatement
    {
        Task<MiniStatementResponseDto> MiniStatementAsync(MiniStatementRequest model, CancellationToken cancellationToken = default);
    }
}

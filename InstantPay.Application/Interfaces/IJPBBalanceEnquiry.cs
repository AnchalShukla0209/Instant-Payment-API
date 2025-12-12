using InstantPay.SharedKernel.RequestPayload;
using InstantPay.SharedKernel.Results;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InstantPay.Application.Interfaces
{
    public interface IJPBBalanceEnquiry
    {
        Task<BalanceInquiryResponseDto> BalanceInquiryAsync(BalanceInquiryRequestDto model, CancellationToken cancellationToken = default);
    }
}

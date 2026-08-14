using InstantPay.SharedKernel.RequestPayload;
using InstantPay.SharedKernel.Results;
using System.Threading;
using System.Threading.Tasks;

namespace InstantPay.Application.Interfaces
{
    public interface IJPBSendNPCIOtp
    {
        Task<JioOtpResponseDto> SendOtpAsync(JioOtpRequest request, CancellationToken cancellationToken = default);
    }
}

using InstantPay.SharedKernel.RequestPayload;
using InstantPay.SharedKernel.Results;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InstantPay.Application.Interfaces
{
    public interface IAEPSService
    {
        Task<JIOAgentLoginResponse> GetJioAgentAEPSLogin(string agentId, string mobile, string AadharNumber);

        Task<JIOAgentLoginResponse> CheckAgentDailyLoginAsync(int UserId);

        Task<CreateAgentResponseDto> CreateAgentAsync(CreateAgentRequestDto model, CancellationToken cancellationToken = default);

        Task<AgentEKYCResponseDto> AgentEKYCAsync(AgentEKYCRequestDto model, CancellationToken cancellationToken = default);
    }
}

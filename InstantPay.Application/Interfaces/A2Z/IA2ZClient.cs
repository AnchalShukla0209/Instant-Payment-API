using InstantPay.SharedKernel.RequestPayload.A2Z;
using InstantPay.SharedKernel.Results.A2Z;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InstantPay.Application.Interfaces.A2Z
{
    public interface IA2ZClient
    {
        Task<A2ZRechargePlanResponse> GetRechargePlansAsync(A2ZRechargePlanRequest payload);
    }
}

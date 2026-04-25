using InstantPay.SharedKernel.Results.PAN;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InstantPay.Application.Interfaces.PAN
{
    public interface IPanService
    {
        Task<PanVerifyResponse> VerifyPanAsync(string panNumber);
    }
}

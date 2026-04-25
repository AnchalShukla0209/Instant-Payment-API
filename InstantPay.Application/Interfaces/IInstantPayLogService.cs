using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InstantPay.Application.Interfaces
{
    public interface IInstantPayLogService
    {
        Task AddLogAsync(string request, string response, string apiMode);
    }
}

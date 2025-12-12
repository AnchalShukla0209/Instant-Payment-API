using InstantPay.Application.DTOs;
using InstantPay.SharedKernel.Entity;
using InstantPay.SharedKernel.Results;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InstantPay.Application.Interfaces
{
    public interface IDashboardService
    {
        Task<DashboardDto> GetDashboardAsync(int userId, string username);
        Task<WalletBalanceDto> GetUserBalance(GetWalletBalanceRequest request);
    }
}

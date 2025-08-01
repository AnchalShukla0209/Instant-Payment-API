using InstantPay.Infrastructure.Sql.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InstantPay.Application.Interfaces
{
    public interface IWalletRepository
    {
        Task<decimal> GetLatestWalletBalanceAsync(int userId);
        Task AddWalletEntryAsync(Tbluserbalance balanceEntry);
    }

}

using InstantPay.Application.Interfaces;
using InstantPay.Infrastructure.Sql.Entities;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InstantPay.Application.Services
{
    public class WalletRepositry : IWalletRepository
    {
        public readonly AppDbContext _context;
        public WalletRepositry(AppDbContext context)
        {
            _context = context;
        }

        public async Task<decimal> GetLatestWalletBalanceAsync(int userId)
        {
          var Amount=  await _context.Tbluserbalances
                    .Where(b => b.UserId == userId)
                    .OrderByDescending(b => b.Id)
                    .Select(b => b.NewBal)
                    .FirstOrDefaultAsync() ?? 0m;
            return Amount;
        }
        public async Task AddWalletEntryAsync(Tbluserbalance balanceEntry)
        {
            if(balanceEntry == null)
            {
                return;
            }
            _context.Tbluserbalances.Add(balanceEntry);
            await _context.SaveChangesAsync();
        }
    }
}

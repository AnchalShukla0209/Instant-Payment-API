using InstantPay.Application.Interfaces;
using InstantPay.Infrastructure.Sql.Entities;
using InstantPay.SharedKernel.Entity;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InstantPay.Application.Services
{
    public class DashboardService: IDashboardService
    {
        private readonly AppDbContext _context;

        public DashboardService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<DashboardDto> GetDashboardAsync(int userId, string username)
        {
            var walletAmount = await _context.Tbluserbalances
                .Where(x => x.UserId == userId && x.UserName.Trim().ToLower()== username.Trim().ToLower())
                .OrderByDescending(x => x.Id)
                .Select(x => x.NewBal)
                .FirstOrDefaultAsync();

            var services = await _context.Tbl_Services
                .Where(x => x.IsActive == true)
                .Select(x => new ServiceDto
                {
                    ServiceName = x.ServiceName ?? "",
                    ServiceImagePath = x.ServicePath ?? ""
                }).ToListAsync();

            var totalTransaction = await _context.TransactionDetails.SumAsync(x => (int?)x.TransId) ?? 0;
            var userJoined = await _context.TblUsers.CountAsync();

            return new DashboardDto
            {
                WalletAmount = (decimal)walletAmount,
                Services = services,
                TotalTransaction = totalTransaction,
                UserJoined = userJoined
            };
        }
    }
}

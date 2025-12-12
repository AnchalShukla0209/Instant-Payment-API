using Azure.Core;
using InstantPay.Application.Interfaces;
using InstantPay.Infrastructure.Sql.Entities;
using InstantPay.SharedKernel.Entity;
using InstantPay.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InstantPay.Application.Services
{
    public class DashboardService : IDashboardService
    {
        private readonly AppDbContext _context;

        public DashboardService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<DashboardDto> GetDashboardAsync(int userId, string username)
        {
            var walletAmount = await _context.Tbluserbalances
                .Where(x => x.UserId == userId)
                .OrderByDescending(x => x.Id)
                .Select(x => x.NewBal)
                .FirstOrDefaultAsync();

            var services = await _context.Tbl_Services
                .Where(x => x.IsDeleted == false)
                .Select(x => new ServiceDto
                {
                    ServiceId = x.Id,
                    ServiceName = x.ServiceName ?? "",
                    ServiceImagePath = x.ServicePath ?? "",
                    ActiveStatus = x.IsActive
                }).ToListAsync();

            var totalTransaction = await _context.TransactionDetails.SumAsync(x => (long?)x.TransId) ?? 0L;

            var userJoined = await _context.TblUsers.CountAsync();

            var userdata = _context.TblUsers.Where(id => id.Id == userId).FirstOrDefault();

            var rawTransactions = await _context.TransactionDetails
                                    .Where(id =>
                                        id.UserId == userId.ToString() &&
                                        id.UserName.Trim().ToLower() ==
                                        (username + "-" + userdata.Phone).Trim().ToLower())
                                    .OrderByDescending(t => t.TransId)
                                    .Take(5)
                                    .ToListAsync();

             var transactionDetails = rawTransactions
                .Select(t => new TransactionDetailsDto
                {
                    UserName = t.UserName,
                    TxnId = t.TxnId ?? "",
                    ServiceName = t.ServiceName,
                    OldBal = t.OldBal ?? 0,
                    Amount = t.Amount ?? 0,
                    NewBal = decimal.TryParse(t.NewBal, out var v) ? v : 0
                })
                .ToList();

            return new DashboardDto
            {
                WalletAmount = walletAmount ?? 0,
                Services = services,
                TotalTransaction = totalTransaction,
                UserJoined = userJoined,
                _TransactionDetails = transactionDetails
            };
        }

        public async Task<WalletBalanceDto> GetUserBalance(GetWalletBalanceRequest request)
        {
            try
            {
                var walletAmount = await _context.Tbluserbalances
                    .Where(x => x.UserId == request.UserId)
                    .OrderByDescending(x => x.Id)
                    .Select(x => x.NewBal)
                    .FirstOrDefaultAsync();

                var result = new WalletBalanceDto
                {
                    UserId = request.UserId,
                    UserName = request.UserName,
                    Balance = (decimal)walletAmount
                };
                return result;
            }
            catch (Exception ex)
            {
                return null;
            }
        }
    }
}

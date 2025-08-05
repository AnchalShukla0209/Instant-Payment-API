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
    public class ClientOperation : IClientOperation
    {
        private readonly AppDbContext _context;
        public ClientOperation(AppDbContext context)
        {
            _context = context;
        }

        public async Task<GetUsersWithMainBalanceResponse> GetClientList(GetUsersWithMainBalanceQuery request)
        {
            DateOnly? fromDate = null;
            DateOnly? toDate = null;

            if (DateOnly.TryParse(request.FromDate, out var parsedFromDate))
                fromDate = parsedFromDate;

            if (DateOnly.TryParse(request.ToDate, out var parsedToDate))
                toDate = parsedToDate;

            var balanceQuery = _context.TblWlbalances.AsQueryable();


            if (fromDate.HasValue)
            {
                balanceQuery = balanceQuery.Where(b => b.Txndate.Value.Date >= fromDate.Value.ToDateTime(TimeOnly.MinValue).Date);
            }
            if (toDate.HasValue)
                balanceQuery = balanceQuery.Where(b => b.Txndate.Value.Date <= toDate.Value.ToDateTime(TimeOnly.MinValue).Date);

            var latestBalances = await balanceQuery
                .GroupBy(b => b.UserId)
                .Select(g => g.OrderByDescending(b => b.Id).FirstOrDefault())
                .ToListAsync();

            var balanceDict = latestBalances.ToDictionary(b => b.UserId, b => b.NewBal);
            var totalBalance = balanceDict.Values.Sum();

            var totalCount = await _context.TblWlUsers.CountAsync();

            var users = await _context.TblWlUsers
                .OrderByDescending(u => u.Id)
                .Skip((request.PageIndex - 1) * request.PageSize)
                .Take(request.PageSize)
                .Select(u => new UserBalanceDto
                {
                    Id = u.Id,
                    UserName = u.UserName ?? "",
                    CompanyName = u.CompanyName ?? "",
                    Domain = u.DomainName ?? "",
                    City = u.City ?? "",
                    Status = u.Status ?? "",
                    EmailId = u.EmailId ?? "",
                    MainBalance = (decimal)(balanceDict.ContainsKey(Convert.ToString(u.Id)) ? balanceDict[Convert.ToString(u.Id)] : 0)
                })
                .ToListAsync();

            return new GetUsersWithMainBalanceResponse
            {
                PageIndex = request.PageIndex,
                PageSize = request.PageSize,
                TotalRecords = totalCount,
                TotalBalance = (decimal)totalBalance,
                Users = users
            };
        }
    }
}

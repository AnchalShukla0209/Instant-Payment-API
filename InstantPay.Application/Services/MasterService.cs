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
    public class MasterService : IMasterService
    {
        private readonly AppDbContext _context;

        public MasterService(AppDbContext context)
        {
            _context = context;
        }

       

        public async Task<ServiceMasterDTO> GetSuperAdminDashboardData(int? ServiceId, int userId, string username, int year)
        {

            try
            {
                var walletAmount = await _context.Tbluserbalances
               .Where(x => x.UserId == userId && x.UserName.Trim().ToLower() == username.Trim().ToLower())
               .OrderByDescending(x => x.Id)
               .Select(x => x.NewBal)
               .FirstOrDefaultAsync();

                var totalTransaction = await _context.TransactionDetails.CountAsync();
                var userJoined = await _context.TblUsers.CountAsync();

                var query = _context.TransactionDetails.AsQueryable();
                query = query.Where(x => x.ReqDate.HasValue && x.ReqDate.Value.Year == year);
                if (ServiceId != 0)
                {
                    query = query.Where(x => x.ServiceId == ServiceId);
                }
                var transactions = await query.ToListAsync();
                var pieData = new List<int>
                {
                    transactions.Count(x => x.Status == "Success"),
                    transactions.Count(x => x.Status == "Pending"),
                    transactions.Count(x => x.Status == "Failed")
                };
                var monthLabels = Enumerable.Range(1, 12)
                    .Select(m => new DateTime(year, m, 1).ToString("MMM"))
                    .ToList();

                var lineData = Enumerable.Range(1, 12)
                    .Select(month => transactions.Count(x => x.ReqDate.Value.Month == month))
                    .ToList();

                
                var services = await _context.Tbl_Services
                    .Where(x => x.IsActive == true)
                    .Select(x => new Service
                    {
                        ServiceName = x.ServiceName ?? "",
                        ServiceId = x.Id
                    }).ToListAsync();

                return new ServiceMasterDTO
                {
                    services = services,
                    walletAmount= (decimal)walletAmount,
                    totalTransection= totalTransaction,
                    totalUserJoined= userJoined,
                    pieData = pieData,
                    lineData = lineData,
                    lineLabels = monthLabels
                };
            }
            catch(Exception ex)
            {
                return new ServiceMasterDTO
                {
                    services = null,
                    walletAmount = 0,
                    totalTransection = 0,
                    totalUserJoined = 0,
                    pieData = null,
                    lineData = null,
                    lineLabels = null
                };
            }
        }
    }
}

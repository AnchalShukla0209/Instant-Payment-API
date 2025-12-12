using InstantPay.Application.Interfaces;
using InstantPay.Infrastructure.Sql.Entities;
using InstantPay.SharedKernel.Entity;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory;
using static System.Runtime.InteropServices.JavaScript.JSType;

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
                var pieData = new List<int>();
                var lineData = new List<int>();
                var walletAmount = await _context.Tbluserbalances
               .Where(x => x.UserId == userId && x.UserName.Trim().ToLower() == username.Trim().ToLower())
               .OrderByDescending(x => x.Id)
               .Select(x => x.NewBal)
               .FirstOrDefaultAsync();

                var totalTransaction = await _context.TransactionDetails.CountAsync();
                var totalTransaction2 = await _context.TblPaymentRequest.CountAsync();
                totalTransaction = totalTransaction + totalTransaction2;
                var userJoined = await _context.TblUsers.CountAsync();
                if (ServiceId == 0 || (ServiceId != 0 && ServiceId != 7))
                {
                    var query = _context.TransactionDetails.AsQueryable();
                    query = query.Where(x => x.ReqDate.HasValue && x.ReqDate.Value.Year == year);

                    if (ServiceId != 0)
                        query = query.Where(x => x.ServiceId == ServiceId);

                    var transactions = await query.ToListAsync();

                    pieData = new List<int>
    {
        transactions.Count(x => x.Status.Equals("Success", StringComparison.OrdinalIgnoreCase)),
        transactions.Count(x => x.Status.Equals("Pending", StringComparison.OrdinalIgnoreCase)),
        transactions.Count(x => x.Status.Equals("Failed", StringComparison.OrdinalIgnoreCase))
    };

                    lineData = Enumerable.Range(1, 12)
                                         .Select(month => transactions.Count(x => x.ReqDate.Value.Month == month))
                                         .ToList();
                }

                // Add wallet transactions if ServiceId == 0 (All) or 7
                if (ServiceId == 0 || ServiceId == 7)
                {
                    var query2 = _context.TblPaymentRequest.AsQueryable();
                    query2 = query2.Where(x => x.CreatedOn.HasValue && x.CreatedOn.Value.Year == year);

                    var wallettrans = await query2.ToListAsync();

                    // If ServiceId == 7, only show wallet transactions
                    if (ServiceId == 7)
                    {
                        pieData = new List<int>
        {
            wallettrans.Count(x => x.Status.Equals("Approved", StringComparison.OrdinalIgnoreCase)),
            wallettrans.Count(x => x.Status.Equals("Pending", StringComparison.OrdinalIgnoreCase)),
            wallettrans.Count(x => x.Status.Equals("Rejected", StringComparison.OrdinalIgnoreCase))
        };

                        lineData = Enumerable.Range(1, 12)
                                             .Select(month => wallettrans.Count(x => x.CreatedOn.Value.Month == month))
                                             .ToList();
                    }
                    else if (ServiceId == 0) // Add wallet counts to existing TransactionDetails counts
                    {
                        var walletPie = new List<int>
        {
            wallettrans.Count(x => x.Status.Equals("Approved", StringComparison.OrdinalIgnoreCase)),
            wallettrans.Count(x => x.Status.Equals("Pending", StringComparison.OrdinalIgnoreCase)),
            wallettrans.Count(x => x.Status.Equals("Rejected", StringComparison.OrdinalIgnoreCase))
        };

                        for (int i = 0; i < 3; i++)
                            pieData[i] += walletPie[i]; // sum counts

                        for (int month = 0; month < 12; month++)
                            lineData[month] += wallettrans.Count(x => x.CreatedOn.Value.Month == month);
                    }
                }

                var monthLabels = Enumerable.Range(1, 12)
                    .Select(m => new DateTime(year, m, 1).ToString("MMM"))
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
                    walletAmount = walletAmount== null?0: (decimal)walletAmount,
                    totalTransection = totalTransaction,
                    totalUserJoined = userJoined,
                    pieData = pieData,
                    lineData = (List<int>)lineData,
                    lineLabels = monthLabels
                };
            }
            catch (Exception ex)
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

        //Mode

        public async Task<IReadOnlyList<UserMasterDataForDD>> GetUserMasterDD(string Mode)
        {
            try
            {
                IQueryable<UserMasterDataForDD> query = Enumerable.Empty<UserMasterDataForDD>().AsQueryable();
                if (Mode == "RET")
                {
                    query = from tonp in _context.TblUsers
                            where tonp.Status.Trim().ToLower() == "active"
                            select new UserMasterDataForDD
                            {
                                Id = tonp.Id,
                                Name = Convert.ToString(tonp.Name)
                            };
                }
                else if (Mode == "AD")
                {
                    query = from tonp in _context.TblWlUsers
                            where tonp.Status.Trim().ToLower() == "active"
                            select new UserMasterDataForDD
                            {
                                Id = tonp.Id,
                                Name = Convert.ToString(tonp.UserName)
                            };
                }
                var data = query.ToList();
                return data;
            }
            catch (Exception ex)
            {
                throw ex;
            }

        }

        public async Task<ServiceStatusResponse> GetServiceStatus(string Mode = "", int UserId = 0)
        {
            try
            {
                switch (Mode)
                {
                    case "AEPS":
                        {
                            var normalizedMode = Mode.Trim().ToLower();

                            bool userServiceActive = _context.TblUsers
                                .Any(u => u.Aeps != null && u.Aeps.Trim().ToLower() == "active" && u.Id== UserId);

                            bool? serviceActive = _context.Tbl_Services
                                .Where(s => s.ServiceName.Trim().ToLower() == normalizedMode && s.IsDeleted== false)
                                .Select(s => s.IsActive)   // only pull the isActive column
                                .FirstOrDefault();         // default(bool) = false

                            return new ServiceStatusResponse
                            {
                                UserServiceActive = userServiceActive,
                                ServiceActive = (bool)serviceActive
                            };
                        }

                    case "MoneyTransfer":
                        {
                            var normalizedMode = Mode.Trim().ToLower();

                            bool userServiceActive = _context.TblUsers
                                .Any(u => u.Aeps != null && u.MoneyTransfer.Trim().ToLower() == "active" && u.Id == UserId);

                            bool? serviceActive = _context.Tbl_Services
                                .Where(s => s.ServiceName.Trim().ToLower() == normalizedMode && s.IsDeleted == false)
                                .Select(s => s.IsActive)  
                                .FirstOrDefault();

                            return new ServiceStatusResponse
                            {
                                UserServiceActive = userServiceActive,
                                ServiceActive = (bool)serviceActive
                            };
                        }

                    case "Recharge":
                        {
                            var normalizedMode = Mode.Trim().ToLower();

                            bool userServiceActive = _context.TblUsers
                                .Any(u => u.Aeps != null && u.MobileRecharge.Trim().ToLower() == "active" && u.Id == UserId);

                            bool? serviceActive = _context.Tbl_Services
                                .Where(s => s.ServiceName.Trim().ToLower() == normalizedMode && s.IsDeleted == false)
                                .Select(s => s.IsActive)
                                .FirstOrDefault();

                            return new ServiceStatusResponse
                            {
                                UserServiceActive = userServiceActive,
                                ServiceActive = (bool)serviceActive
                            };
                        }

                    case "Bill Payment":
                        {
                            var normalizedMode = Mode.Trim().ToLower();

                            bool userServiceActive = _context.TblUsers
                                .Any(u => u.Aeps != null && u.BillPayment.Trim().ToLower() == "active" && u.Id == UserId);

                            bool? serviceActive = _context.Tbl_Services
                                .Where(s => s.ServiceName.Trim().ToLower() == normalizedMode && s.IsDeleted == false)
                                .Select(s => s.IsActive)
                                .FirstOrDefault();

                            return new ServiceStatusResponse
                            {
                                UserServiceActive = userServiceActive,
                                ServiceActive = (bool)serviceActive
                            };

                        }

                       default:
                        {
                            return null;
                        }


                }


            }
            catch (Exception ex)
            {
                throw ex;
            }

        }
    }
}

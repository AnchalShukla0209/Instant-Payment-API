using InstantPay.Application.Interfaces;
using InstantPay.Application.Interfaces.A2Z;
using InstantPay.Infrastructure.Sql.Entities;
using InstantPay.SharedKernel.Entity;
using InstantPay.SharedKernel.RequestPayload;
using InstantPay.SharedKernel.RequestPayload.A2Z;
using InstantPay.SharedKernel.Results;
using InstantPay.SharedKernel.Results.A2Z;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace InstantPay.Application.Services
{
    public class MasterService : IMasterService
    {
        private readonly AppDbContext _context;
        private readonly IMPlanClient _mPlanClient;
        private readonly IA2ZClient _a2zClient;
        private readonly IWalletService _walletService;
        public MasterService(AppDbContext context, IMPlanClient mPlanClient, IA2ZClient a2zclient, IWalletService walletService)
        {
            _context = context;
            _mPlanClient = mPlanClient;
            _a2zClient = a2zclient;
            _walletService = walletService;
        }
        public async Task<ServiceMasterDTO> GetSuperAdminDashboardData(int? ServiceId, int userId, string username, int year)
        {
            try
            {
                var pieData = new List<int>() { 0, 0, 0 };
                var lineData = Enumerable.Repeat(0, 12).ToList();

                var walletAmount = await _walletService.GetBalanceAsync(userId);

                var totalTransaction1 = await _context.TransactionDetails.CountAsync();
                var totalTransaction2 = await _context.TblPaymentRequest.CountAsync();
                var totalTransaction = totalTransaction1 + totalTransaction2;

                var userJoined = await _context.TblUsers.CountAsync();

                // main transaction query
                if (ServiceId == 0 || (ServiceId != 0 && ServiceId != 7))
                {
                    var query = _context.TransactionDetails
                        .Where(x => x.ReqDate.HasValue && x.ReqDate.Value.Year == year);

                    if (ServiceId != 0)
                        query = query.Where(x => x.ServiceId == ServiceId);

                    // status counts
                    var statusCounts =
                        await query.GroupBy(x => x.Status.ToLower())
                                   .Select(g => new { Status = g.Key, Count = g.Count() })
                                   .ToDictionaryAsync(x => x.Status, x => x.Count);

                    pieData[0] = statusCounts.TryGetValue("success", out var s) ? s : 0;
                    pieData[1] = statusCounts.TryGetValue("pending", out var p) ? p : 0;
                    pieData[2] = statusCounts.TryGetValue("failed", out var f) ? f : 0;

                    // monthly counts
                    var monthCounts =
                        await query.GroupBy(x => x.ReqDate.Value.Month)
                                   .Select(g => new { Month = g.Key, Count = g.Count() })
                                   .ToDictionaryAsync(x => x.Month, x => x.Count);

                    for (int m = 1; m <= 12; m++)
                        lineData[m - 1] = monthCounts.TryGetValue(m, out var val) ? val : 0;
                }

                // wallet transactions
                if (ServiceId == 0 || ServiceId == 7)
                {
                    var query2 = _context.TblPaymentRequest
                        .Where(x => x.CreatedOn.HasValue && x.CreatedOn.Value.Year == year);

                    var walletStatusCounts =
                        await query2.GroupBy(x => x.Status.ToLower())
                                    .Select(g => new { Status = g.Key, Count = g.Count() })
                                    .ToDictionaryAsync(x => x.Status, x => x.Count);

                    var walletMonthlyCounts =
                        await query2.GroupBy(x => x.CreatedOn.Value.Month)
                                    .Select(g => new { Month = g.Key, Count = g.Count() })
                                    .ToDictionaryAsync(x => x.Month, x => x.Count);

                    if (ServiceId == 7)
                    {
                        pieData[0] = walletStatusCounts.TryGetValue("approved", out var a) ? a : 0;
                        pieData[1] = walletStatusCounts.TryGetValue("pending", out var p) ? p : 0;
                        pieData[2] = walletStatusCounts.TryGetValue("rejected", out var r) ? r : 0;

                        for (int m = 1; m <= 12; m++)
                            lineData[m - 1] = walletMonthlyCounts.TryGetValue(m, out var val) ? val : 0;
                    }
                    else if (ServiceId == 0)
                    {
                        pieData[0] += walletStatusCounts.TryGetValue("approved", out var a) ? a : 0;
                        pieData[1] += walletStatusCounts.TryGetValue("pending", out var p) ? p : 0;
                        pieData[2] += walletStatusCounts.TryGetValue("rejected", out var r) ? r : 0;

                        for (int m = 1; m <= 12; m++)
                            lineData[m - 1] += walletMonthlyCounts.TryGetValue(m, out var val) ? val : 0;
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
                    walletAmount = walletAmount,
                    totalTransection = totalTransaction,
                    totalUserJoined = userJoined,
                    pieData = pieData,
                    lineData = lineData,
                    lineLabels = monthLabels
                };
            }
            catch
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
                                Name = Convert.ToString(tonp.Name+"-"+tonp.Phone)
                            };
                }
                else if (Mode == "AD")
                {
                    query = from tonp in _context.TblWlUsers
                            where tonp.Status.Trim().ToLower() == "active"
                            select new UserMasterDataForDD
                            {
                                Id = tonp.Id,
                                Name = Convert.ToString(tonp.UserName+ "-" + tonp.Phone)
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
                                .Any(u => u.Aeps != null && u.Aeps.Trim().ToLower() == "active" && u.Id == UserId);

                            var serviceData = _context.Tbl_Services
                                .Where(s => s.ServiceName.Trim().ToLower() == normalizedMode && s.IsDeleted == false)
                                .Select(s => new { s.IsActive, s.isActiveOnApk })
                                .FirstOrDefault();

                            return new ServiceStatusResponse
                            {
                                UserServiceActive = userServiceActive,
                                ServiceActive = serviceData?.IsActive ?? false,
                                ServiceActiveOnApk = serviceData?.isActiveOnApk ?? false
                            };
                        }

                    case "MoneyTransfer":
                        {
                            var normalizedMode = Mode.Trim().ToLower();

                            bool userServiceActive = _context.TblUsers
                                .Any(u => u.Aeps != null && u.MoneyTransfer.Trim().ToLower() == "active" && u.Id == UserId);

                            var serviceData = _context.Tbl_Services
                                .Where(s => s.ServiceName.Trim().ToLower() == normalizedMode && s.IsDeleted == false)
                                .Select(s => new { s.IsActive, s.isActiveOnApk })
                                .FirstOrDefault();

                            return new ServiceStatusResponse
                            {
                                UserServiceActive = userServiceActive,
                                ServiceActive = serviceData?.IsActive ?? false,
                                ServiceActiveOnApk = serviceData?.isActiveOnApk ?? false
                            };
                        }

                    case "Recharge":
                        {
                            var normalizedMode = Mode.Trim().ToLower();

                            bool userServiceActive = _context.TblUsers
                                .Any(u => u.Aeps != null && u.MobileRecharge.Trim().ToLower() == "active" && u.Id == UserId);

                            var serviceData = _context.Tbl_Services
                                .Where(s => s.ServiceName.Trim().ToLower() == normalizedMode && s.IsDeleted == false)
                                .Select(s => new { s.IsActive, s.isActiveOnApk })
                                .FirstOrDefault();

                            return new ServiceStatusResponse
                            {
                                UserServiceActive = userServiceActive,
                                ServiceActive = serviceData?.IsActive ?? false,
                                ServiceActiveOnApk = serviceData?.isActiveOnApk ?? false
                            };
                        }

                    case "Bill Payment":
                        {
                            var normalizedMode = Mode.Trim().ToLower();

                            bool userServiceActive = _context.TblUsers
                                .Any(u => u.Aeps != null && u.BillPayment.Trim().ToLower() == "active" && u.Id == UserId);

                            var serviceData = _context.Tbl_Services
                                .Where(s => s.ServiceName.Trim().ToLower() == normalizedMode && s.IsDeleted == false)
                                .Select(s => new { s.IsActive, s.isActiveOnApk })
                                .FirstOrDefault();

                            return new ServiceStatusResponse
                            {
                                UserServiceActive = userServiceActive,
                                ServiceActive = serviceData?.IsActive ?? false,
                                ServiceActiveOnApk = serviceData?.isActiveOnApk ?? false
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

        public async Task<string> GetRechargePlans(PlanRequestPayload payload)
        {
            return await _mPlanClient.GetRechargePlansAsync(payload);
        }

        public async Task<string> GetRechargePlansNew(PlanRequestPayload payload)
        {
            return await _mPlanClient.GetRechargePlansNewAsync(payload);
        }

        public async Task<A2ZRechargePlanResponse> GetRechargePlan(PlanRequestPayload payload)
        {
            var request = new A2ZRechargePlanRequest
            {
                mobile_number = payload.tel,
                provider = payload.operatorName
            };

            return await _a2zClient.GetRechargePlansAsync(request);
        }

        public async Task<List<ServiceDTO>> GetServices()
        {
            return await _context.Tbl_Services
                .AsNoTracking()
                .Where(x => x.IsActive == true && x.IsDeleted == false)
                .OrderBy(x => x.Id)
                .Select(x => new ServiceDTO
                {
                    key = x.CategoryCode,
                    label = x.ServiceName,
                    icon = x.Icon,
                    isActiveOnWeb = x.IsActive,
                    isActiveOnApk = x.isActiveOnApk
                })
                .ToListAsync();
        }

        public async Task<List<ProviderDTO>> GetProviders(string serviceCode)
        {
            if (string.Equals(serviceCode, "SETTLEMENT", StringComparison.OrdinalIgnoreCase))
            {
                var mappings = await _context.SERVICE_PROVIDER.AsNoTracking()
                    .Where(x => x.ServiceCode != null && x.ServiceCode.ToUpper() == "SETTLEMENT")
                    .ToListAsync();
                return new List<ProviderDTO>
                {
                    new()
                    {
                        key = "RKIT", label = "RechargeKit",
                        isEnabled = mappings.Count == 0 || mappings.Any(x => x.ProviderCode != null && x.ProviderCode.ToUpper() == "RKIT" && x.IsEnabled == true)
                    },
                    new()
                    {
                        key = "RBL", label = "RBL Bank",
                        isEnabled = mappings.Any(x => x.ProviderCode != null && x.ProviderCode.ToUpper() == "RBL" && x.IsEnabled == true)
                    }
                };
            }

            var providers = await (
                from sp in _context.SERVICE_PROVIDER.AsNoTracking()
                join p in _context.MASTER_PROVIDER.AsNoTracking()
                    on sp.ProviderCode equals p.ProviderCode
                where sp.ServiceCode == serviceCode
                select new ProviderDTO
                {
                    key = p.ProviderCode,
                    label = p.ProviderName,
                    isEnabled = sp.IsEnabled 
                }
            ).ToListAsync();

            return providers;
        }

        public async Task<List<FeatureDto>> GetFeatures(string serviceCode)
        {
            if (serviceCode == "AEPS")
            {
                return await (from mf in _context.MASTER_FEATURE.AsNoTracking()
                              join spfm in _context.SERVICE_PROVIDER_FEATURE_MAP
                              on mf.FeatureCode equals spfm.FeatureCode
                              where mf.ServiceCode == serviceCode
                              orderby mf.DisplayOrder
                              select new FeatureDto
                              {
                                  Key = mf.FeatureCode,
                                  Label = mf.FeatureName,
                                  Icon = mf.Icon,
                                  Config = mf.ExtraConfig,
                                  isEnabled = spfm.IsEnabled,
                                  ProviderCode = spfm.ProviderCode   // extra column
                              }).ToListAsync();
            }
            else
            {
                return await _context.MASTER_FEATURE
                    .AsNoTracking()
                    .Where(x => x.ServiceCode == serviceCode)
                    .OrderBy(x => x.DisplayOrder)
                    .Select(x => new FeatureDto
                    {
                        Key = x.FeatureCode,
                        Label = x.FeatureName,
                        Icon = x.Icon,
                        Config = x.ExtraConfig,
                        isEnabled = x.IsEnabled,
                        ProviderCode = null // or default
                    })
                    .ToListAsync();
            }
        }

        public async Task<List<FeatureDto>> GetProviderFeatures(string serviceCode, string providerCode)
        {
            var providerEnabled = await _context.SERVICE_PROVIDER
         .AsNoTracking()
         .Where(x => x.ServiceCode == serviceCode && x.ProviderCode == providerCode)
         .Select(x => x.IsEnabled)
         .FirstOrDefaultAsync();

            if (providerEnabled == false)
                return new List<FeatureDto>(); // Provider disabled → no features

            return await (
                from f in _context.MASTER_FEATURE.AsNoTracking()
                join m in _context.SERVICE_PROVIDER_FEATURE_MAP.AsNoTracking()
                    on f.FeatureCode equals m.FeatureCode
                where f.ServiceCode == serviceCode
                      && m.ServiceCode == serviceCode
                      && m.ProviderCode == providerCode
                orderby f.DisplayOrder
                select new FeatureDto
                {
                    Key = f.FeatureCode,
                    Label = f.FeatureName,
                    Icon = f.Icon,
                    Config = f.ExtraConfig,
                    isEnabled = m.IsEnabled
                }
            ).ToListAsync();
        }

        public async Task<ResponseSuccess> ToggleProvider(ToggleRequestDto req)
        {
            var provider = await _context.MASTER_PROVIDER
        .FirstOrDefaultAsync(x => x.ProviderCode == req.ProviderCode);

            if (provider == null)
                return new ResponseSuccess { success = false, message = "Provider not found" };

            provider.IsEnabled = req.IsEnabled;
            await _context.SaveChangesAsync();

            return new ResponseSuccess { success = true, message = "Provider Updated" };

        }

        public async Task<ResponseSuccess> ToggleFeature(ToggleRequestDto req)
        {
            var feature = await _context.MASTER_FEATURE.FirstOrDefaultAsync(x =>
            x.ServiceCode == req.ServiceCode &&
            x.FeatureCode == req.FeatureCode);
            if (feature == null)
            {
                return new ResponseSuccess()
                {
                    success = false,
                    message = "Feature not found"
                };
            }
            feature.IsEnabled = req.IsEnabled;
            await _context.SaveChangesAsync();
            return new ResponseSuccess()
            {
                success = true,
                message = "Feature Updated"
            };

        }

        public async Task<ResponseSuccess> ToggleProviderFeature(ToggleRequestDto req)
        {
            var map = await _context.SERVICE_PROVIDER_FEATURE_MAP
        .FirstOrDefaultAsync(x =>
            x.ServiceCode == req.ServiceCode &&
            x.ProviderCode == req.ProviderCode &&
            x.FeatureCode == req.FeatureCode);

            if (map == null)
                return new ResponseSuccess { success = false, message = "Mapping not found" };

            map.IsEnabled = req.IsEnabled;
            await _context.SaveChangesAsync();

            return new ResponseSuccess { success = true, message = "Provider Feature Updated" };

        }

        public async Task<ResponseSuccess> ToggleServiceProvider(ToggleRequestDto req)
        {
            var row = await _context.SERVICE_PROVIDER.FirstOrDefaultAsync(x => x.ServiceCode == req.ServiceCode && x.ProviderCode == req.ProviderCode);

            if (row == null)
            {
                var isSettlementProvider = string.Equals(req.ServiceCode, "SETTLEMENT", StringComparison.OrdinalIgnoreCase)
                    && (string.Equals(req.ProviderCode, "RKIT", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(req.ProviderCode, "RBL", StringComparison.OrdinalIgnoreCase));
                if (!isSettlementProvider)
                    return new ResponseSuccess { success = false, message = "Service Provider mapping not found" };

                row = new ServiceProvider
                {
                    ServiceCode = "SETTLEMENT",
                    ProviderCode = req.ProviderCode!.Trim().ToUpperInvariant(),
                    IsEnabled = false
                };
                _context.SERVICE_PROVIDER.Add(row);
                await _context.SaveChangesAsync();
            }

            // Settlement payouts have exactly one active provider. Enabling a provider
            // atomically disables the other settlement providers for a real switch.
            if (req.IsEnabled && string.Equals(req.ServiceCode, "SETTLEMENT", StringComparison.OrdinalIgnoreCase))
            {
                var settlementProviders = await _context.SERVICE_PROVIDER
                    .Where(x => x.ServiceCode != null && x.ServiceCode.ToUpper() == "SETTLEMENT")
                    .ToListAsync();
                foreach (var provider in settlementProviders)
                    provider.IsEnabled = provider.Id == row.Id;
            }
            else
            {
                row.IsEnabled = req.IsEnabled;
            }
            await _context.SaveChangesAsync();

            return new ResponseSuccess
            {
                success = true,
                message = "Service Provider Updated"
            };
        }

    }
}

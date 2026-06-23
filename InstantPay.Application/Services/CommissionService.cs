using InstantPay.Application.Interfaces;
using InstantPay.Infrastructure.Sql.Entities;
using Microsoft.EntityFrameworkCore;

namespace InstantPay.Application.Services
{
    public class CommissionService : ICommissionService
    {
        private readonly AppDbContext _context;
        private readonly IWalletService _walletService;

        public CommissionService(AppDbContext context, IWalletService walletService)
        {
            _context = context;
            _walletService = walletService;
        }

        public async Task<decimal> GetCommissionFromPlanAsync(
            int planId, decimal amount, int serviceId, string apiCode, string shareColumn)
        {
            var allCommissionPlans = await _context.CommissionPlans
                .Where(x => x.PlanId == planId
                    && x.ServiceId == serviceId
                    && x.APICode == apiCode)
                .ToListAsync();

            if (allCommissionPlans == null || !allCommissionPlans.Any()) return 0m;

            CommissionPlan commissionPlan = null;
            foreach (var plan in allCommissionPlans)
            {
                var rangeParts = plan.SlabRange.Split('-');
                if (rangeParts.Length != 2) continue;

                if (!decimal.TryParse(rangeParts[0], out decimal minAmount) ||
                    !decimal.TryParse(rangeParts[1], out decimal maxAmount))
                    continue;

                if (amount >= minAmount && amount <= maxAmount)
                {
                    commissionPlan = plan;
                    break;
                }
            }

            if (commissionPlan == null) return 0m;

            decimal share = shareColumn switch
            {
                "RT"    => commissionPlan.RtShare,
                "AD"    => commissionPlan.AdShare,
                "MD"    => commissionPlan.MdShare,
                "WL"    => commissionPlan.WlAdminShare,
                "ADMIN" => commissionPlan.AdminShare,
                _       => 0m
            };

            decimal commission = commissionPlan.CommissionType.ToLower() == "flat"
                ? share
                : (amount * share / 100);

            return commission;
        }

        public async Task DistributeCommissionAsync(
            TransactionDetail tx, TblUser user, decimal amount, int planId,
            int serviceId, string apiCode, string remarksPrefix)
        {
            decimal rtComm    = await GetCommissionFromPlanAsync(planId, amount, serviceId, apiCode, "RT");
            decimal adComm    = await GetCommissionFromPlanAsync(planId, amount, serviceId, apiCode, "AD");
            decimal mdComm    = await GetCommissionFromPlanAsync(planId, amount, serviceId, apiCode, "MD");
            decimal wlComm    = await GetCommissionFromPlanAsync(planId, amount, serviceId, apiCode, "WL");
            decimal adminComm = await GetCommissionFromPlanAsync(planId, amount, serviceId, apiCode, "ADMIN");

            string remarks = $"{remarksPrefix} TXN:{tx.TxnId}";

            bool hasAd    = !string.IsNullOrEmpty(user.Adid) && user.Adid != "0";
            bool hasMd    = !string.IsNullOrEmpty(user.Mdid) && user.Mdid != "0";
            bool hasWl    = !string.IsNullOrEmpty(user.Wlid) && user.Wlid != "0";
            bool hasAdmin = user.SuperAdminId.HasValue && user.SuperAdminId > 0;

            // rollingBase tracks the commission level of the last parent that actually received a share.
            // If a parent is absent its potential share rolls down to the next existing parent.
            decimal rollingBase = rtComm;

            if (hasAd)
            {
                decimal adDifferential = rollingBase - adComm;
                if (adDifferential > 0)
                {
                    int adId = Convert.ToInt32(user.Adid);
                    await _walletService.CreditAsync(
                        adId, user.Adid,
                        adDifferential, adDifferential, 0, 0,
                        "Commission_Credit",
                        remarks,
                        user.Wlid);
                    tx.AdComm = adDifferential;
                }
                rollingBase = adComm;
            }

            if (hasMd)
            {
                decimal mdDifferential = rollingBase - mdComm;
                if (mdDifferential > 0)
                {
                    int mdId = Convert.ToInt32(user.Mdid);
                    await _walletService.CreditAsync(
                        mdId, user.Mdid,
                        mdDifferential, mdDifferential, 0, 0,
                        "Commission_Credit",
                        remarks,
                        user.Wlid);
                    tx.MdComm = mdDifferential;
                }
                rollingBase = mdComm;
            }

            if (hasWl)
            {
                decimal wlDifferential = rollingBase - wlComm;
                if (wlDifferential > 0)
                {
                    int wlId = Convert.ToInt32(user.Wlid);
                    var wlBalance = await _context.TblWlbalances
                        .Where(x => x.UserId == Convert.ToString(wlId))
                        .OrderByDescending(x => x.Id)
                        .FirstOrDefaultAsync();

                    decimal wlOldBal = wlBalance?.NewBal ?? 0m;
                    decimal wlNewBal = wlOldBal + wlDifferential;

                    _context.TblWlbalances.Add(new TblWlbalance
                    {
                        UserId    = Convert.ToString(wlId),
                        UserName  = user.Wlid,
                        OldBal    = wlOldBal,
                        Amount    = wlDifferential,
                        NewBal    = wlNewBal,
                        TxnType   = "Commission_Credit",
                        CrdrType  = "CR",
                        Remarks   = remarks,
                        Txndate   = DateTime.Now,
                        TxnAmount = wlDifferential,
                        SurComm   = 0,
                        Tds       = 0
                    });

                    tx.WlComm = wlDifferential;
                }
                rollingBase = wlComm;
            }

            if (hasAdmin)
            {
                decimal adminDifferential = rollingBase - adminComm;
                if (adminDifferential > 0)
                {
                    int superAdminId = user.SuperAdminId.Value;
                    await _walletService.SuperAdminCreditAsync(
                        superAdminId, superAdminId.ToString(),
                        adminDifferential, adminDifferential, 0, 0,
                        "Commission_Credit",
                        remarks);
                    tx.SuperAdminShare = adminDifferential;
                }
            }

            await _context.SaveChangesAsync();
        }
    }
}

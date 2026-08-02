using InstantPay.Application.Interfaces.FinoAeps;
using InstantPay.Infrastructure.Sql.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace InstantPay.Application.Services.FinoAeps
{
    public class FinoAepsCommissionService : IFinoAepsCommissionService
    {
        private readonly AppDbContext _context;
        private readonly ILogger<FinoAepsCommissionService> _logger;

        public FinoAepsCommissionService(AppDbContext context, ILogger<FinoAepsCommissionService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<FinoAepsCommission> CalculateCommissionAsync(int userId, decimal amount, string txnType, CancellationToken ct = default)
        {
            var user = await _context.TblUsers
                .Where(u => u.Id == userId)
                .Select(u => new { u.PlanId, u.Usertype, u.Wlid, u.Mdid, u.Adid })
                .FirstOrDefaultAsync(ct);

            if (user == null)
                return new FinoAepsCommission();

            int planId = Convert.ToInt32(user.PlanId ?? "0");
            int slabId = GetSlabId(txnType, amount);

            if (slabId == 0 || planId == 0)
                return new FinoAepsCommission { SlabId = slabId };

            decimal rtComm = await GetCommissionAsync(planId, slabId, amount, "RTShare", ct);
            decimal adComm = await GetCommissionAsync(planId, slabId, amount, "ADShare", ct);
            decimal mdComm = await GetCommissionAsync(planId, slabId, amount, "MDShare", ct);
            decimal wlComm = await GetCommissionAsync(planId, slabId, amount, "WlShare", ct);

            // Adjust commissions based on hierarchy (same as old code logic)
            decimal adjustedAdComm = adComm;
            decimal adjustedMdComm = mdComm;
            decimal adjustedWlComm = wlComm;

            if (user.Usertype == "RT")
            {
                if (user.Adid == "0" && user.Mdid == "0")
                {
                    adjustedWlComm = wlComm - rtComm;
                    adjustedAdComm = 0;
                    adjustedMdComm = 0;
                }
                else if (user.Adid == "0" && user.Mdid != "0")
                {
                    adjustedMdComm = mdComm - rtComm;
                    adjustedWlComm = wlComm - mdComm;
                    adjustedAdComm = 0;
                }
                else if (user.Adid != "0" && user.Mdid != "0")
                {
                    adjustedAdComm = adComm - rtComm;
                    adjustedMdComm = mdComm - adComm;
                    adjustedWlComm = wlComm - mdComm;
                }
                else if (user.Adid != "0" && user.Mdid == "0")
                {
                    adjustedAdComm = adComm - rtComm;
                    adjustedMdComm = 0;
                    adjustedWlComm = wlComm - adComm;
                }
            }

            decimal tds = rtComm * 2 / 100;
            decimal cost = (amount + rtComm) - tds;

            return new FinoAepsCommission
            {
                RetailerCommission = rtComm,
                MdCommission = adjustedMdComm,
                AdCommission = adjustedAdComm,
                WlCommission = adjustedWlComm,
                Tds = tds,
                Cost = cost,
                SlabId = slabId
            };
        }

        private int GetSlabId(string txnType, decimal amount)
        {
            // Slab mapping based on old code
            if (txnType == "CD") // Cash Deposit
            {
                if (amount == 0) return 0;
                if (amount <= 1000) return 47;
                if (amount <= 2000) return 48;
                if (amount == 10000) return 50;
            }
            else if (txnType == "CW") // Cash Withdrawal
            {
                if (amount == 0) return 0;
                if (amount <= 500) return 47;
                if (amount <= 2999) return 48;
                if (amount == 3000) return 49;
                if (amount <= 10000) return 50;
            }
            // For other txn types (BE, MS, AP), no commission or slab 0
            return 0;
        }

        private async Task<decimal> GetCommissionAsync(int planId, int slabId, decimal amount, string shareColumn, CancellationToken ct)
        {
            var slab = await _context.Tblcommissionslabs
                .Where(s => s.PlanId == Convert.ToString(planId) && s.SlabId == Convert.ToString(slabId))
                .Select(s => new { s.Rtshare, s.Adshare, s.Mdshare, s.WlShare, s.CommissionType })
                .FirstOrDefaultAsync(ct);

            if (slab == null)
                return 0;

            decimal share = shareColumn switch
            {
                "RTShare" => slab.Rtshare ?? 0,
                "ADShare" => slab.Adshare ?? 0,
                "MDShare" => slab.Mdshare ?? 0,
                "WlShare" => slab.WlShare ?? 0,
                _ => 0
            };

            if (slab.CommissionType == "RS")
                return share;

            return amount * share / 100;
        }
    }
}

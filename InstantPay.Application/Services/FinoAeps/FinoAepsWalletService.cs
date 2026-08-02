using InstantPay.Application.Interfaces;
using InstantPay.Application.Interfaces.FinoAeps;
using InstantPay.Infrastructure.Sql.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace InstantPay.Application.Services.FinoAeps
{
    public class FinoAepsWalletService : IFinoAepsWalletService
    {
        private readonly AppDbContext _context;
        private readonly IWalletRepository _walletRepo;
        private readonly IFinoAepsCommissionService _commissionService;
        private readonly ILogger<FinoAepsWalletService> _logger;
        private readonly IWalletService _walletService;

        public FinoAepsWalletService(
            AppDbContext context,
            IWalletRepository walletRepo,
            IFinoAepsCommissionService commissionService,
            ILogger<FinoAepsWalletService> logger,
            IWalletService walletService)
        {
            _context = context;
            _walletRepo = walletRepo;
            _commissionService = commissionService;
            _logger = logger;
            _walletService = walletService;
        }

        public async Task<decimal> DebitAsync(int userId, decimal amount, decimal txnamount, decimal charge, string txnType, string bankName, string txnId, CancellationToken ct = default)
        {
            try
            {
                var user = await _context.TblUsers
                    .Where(u => u.Id == userId)
                    .Select(u => new { u.Name, u.Phone, u.Wlid, u.Mdid, u.Adid })
                    .FirstOrDefaultAsync(ct);

                if (user == null)
                {
                    _logger.LogWarning("FinoAepsWalletService.DebitAsync: user {UserId} not found", userId);
                    return 0;
                }

                var (_, newBal, _) = await _walletService.DebitAsync(
                    userId, $"{user.Name}-{user.Phone}",
                    txnamount, amount, charge, 0,
                    $"AEPS {txnType}",
                    $"AEPS {txnType} | Bank: {bankName} | TxnId: {txnId}",
                    user.Wlid?.ToString(), ct);
                return newBal;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "FinoAepsWalletService.DebitAsync failed for UserId={UserId}", userId);
                return 0;
            }
        }

        public async Task<decimal> CreditAsync(int userId, decimal amount, string txnType, string bankName, string txnId, bool isFINOBank = false, CancellationToken ct = default)
        {
            try
            {
                var user = await _context.TblUsers
                    .Where(u => u.Id == userId)
                    .Select(u => new { u.Name, u.Phone, u.Wlid, u.Mdid, u.Adid })
                    .FirstOrDefaultAsync(ct);

                if (user == null)
                {
                    _logger.LogWarning("FinoAepsWalletService.CreditAsync: user {UserId} not found", userId);
                    return 0;
                }

                var (_, newBal, _) = await _walletService.CreditAsync(
                    userId, $"{user.Name}-{user.Phone}",
                    amount, amount, 0, 0,
                    $"AEPS {txnType}",
                    $"FINO AEPS {txnType} | Bank: {bankName} | TxnId: {txnId}",
                    user.Wlid?.ToString(), ct);
                if (txnType.ToUpper() != "AP" && !isFINOBank)
                {
                    await DistributeCommissionAsync(userId, amount, txnType, bankName, user.Wlid, user.Mdid, user.Adid, ct);
                }
                return newBal;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "FinoAepsWalletService.CreditAsync failed for UserId={UserId}", userId);
                return 0;
            }
        }

        private async Task DistributeCommissionAsync(
            int userId, decimal txnAmount, string txnType, string bankName,
            object? wlId, object? mdId, object? adId, CancellationToken ct)
        {
            var commission = await _commissionService.CalculateCommissionAsync(userId, txnAmount, txnType, ct);

            var receivers = new List<(int Id, string Label, decimal CommAmt)>();

            if (int.TryParse(wlId?.ToString(), out int wlIdInt) && wlIdInt > 0 && commission.WlCommission > 0)
                receivers.Add((wlIdInt, "WL", commission.WlCommission));
            if (int.TryParse(mdId?.ToString(), out int mdIdInt) && mdIdInt > 0 && commission.MdCommission > 0)
                receivers.Add((mdIdInt, "MD", commission.MdCommission));
            if (int.TryParse(adId?.ToString(), out int adIdInt) && adIdInt > 0 && commission.AdCommission > 0)
                receivers.Add((adIdInt, "AD", commission.AdCommission));

            if (!receivers.Any()) return;

            var receiverIds = receivers.Select(r => r.Id).ToList();
            var userInfos = await _context.TblUsers
                .Where(u => receiverIds.Contains(u.Id))
                .Select(u => new { u.Id, u.Name, u.Phone, u.Wlid })
                .ToListAsync(ct);

            foreach (var (rid, label, commAmt) in receivers)
            {
                var info = userInfos.FirstOrDefault(u => u.Id == rid);
                if (info == null) continue;

                decimal tds = commAmt * 0.05m;
                decimal netComm = commAmt - tds;

                await _walletService.CreditAsync(
                    rid, $"{info.Name}-{info.Phone}",
                    txnAmount, netComm, commAmt, tds,
                    "Commission",
                    $"AEPS {txnType} Commission | Bank: {bankName} | {label}",
                    info.Wlid?.ToString(), ct);
            }
        }

        public async Task<decimal> GetLatestWalletBalanceAsync(int userId, CancellationToken ct = default)
        {
            return await _walletRepo.GetLatestWalletBalanceAsync(userId);
        }
    }
}

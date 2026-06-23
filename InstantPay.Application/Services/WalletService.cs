using InstantPay.Application.Interfaces;
using InstantPay.Infrastructure.Sql.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Data;

namespace InstantPay.Application.Services
{
    /// <summary>
    /// THE single generic file for all wallet balance operations (Credit / Debit / Refund).
    ///
    /// Root-cause fix for TOCTOU race condition:
    ///   Two concurrent requests for the same user both read the same OldBal before
    ///   either one writes its new record (17 ms apart in the reported incident),
    ///   causing the second entry to carry a stale OldBal and corrupting the ledger.
    ///
    /// Two-layer protection:
    ///   Layer 1 — Per-user SemaphoreSlim (static ConcurrentDictionary).
    ///             Ensures only ONE wallet mutation runs at a time per userId at the
    ///             application level. Works for single-server deployments.
    ///
    ///   Layer 2 — SQL "WITH (UPDLOCK, ROWLOCK)" on the balance SELECT.
    ///             The row-level update lock is held for the duration of the
    ///             enclosing transaction, so a concurrent DB connection (different
    ///             app server) cannot read the same row until this insert commits.
    ///             Works for multi-server deployments.
    ///
    /// To add any new wallet operation in the future, add it HERE ONLY.
    /// </summary>
    public class WalletService : IWalletService
    {
        // One semaphore per userId; created on first use, lives for the process lifetime.
        private static readonly ConcurrentDictionary<int, SemaphoreSlim> _userLocks = new();

        // Separate lock dict for super-admin IDs (different ID space).
        private static readonly ConcurrentDictionary<int, SemaphoreSlim> _superAdminLocks = new();

        private readonly AppDbContext _context;
        private readonly ILogger<WalletService> _logger;

        public WalletService(AppDbContext context, ILogger<WalletService> logger)
        {
            _context = context;
            _logger = logger;
        }

        // ── Public API ────────────────────────────────────────────────────────

        public Task<(decimal OldBalance, decimal NewBalance, int EntryId)> CreditAsync(
            int userId, string userName, decimal txnAmount, decimal amount,
            decimal surComm, decimal tds, string txnType, string remarks,
            string? wlId = null, CancellationToken ct = default) =>
            ApplyAsync(userId, userName, txnAmount, amount, surComm, tds,
                       txnType, "Credit", remarks, wlId, isCredit: true, ct);

        public Task<(decimal OldBalance, decimal NewBalance, int EntryId)> DebitAsync(
            int userId, string userName, decimal txnAmount, decimal amount,
            decimal surComm, decimal tds, string txnType, string remarks,
            string? wlId = null, CancellationToken ct = default) =>
            ApplyAsync(userId, userName, txnAmount, amount, surComm, tds,
                       txnType, "Debit", remarks, wlId, isCredit: false, ct);

        public async Task<decimal> GetBalanceAsync(int userId, CancellationToken ct = default) =>
            await _context.Tbluserbalances
                .Where(b => b.UserId == userId)
                .OrderByDescending(b => b.Id)
                .Select(b => b.NewBal)
                .FirstOrDefaultAsync(ct) ?? 0m;

        // ── Core atomic implementation ─────────────────────────────────────────

        private async Task<(decimal OldBalance, decimal NewBalance, int EntryId)> ApplyAsync(
            int userId, string userName, decimal txnAmount, decimal amount,
            decimal surComm, decimal tds, string txnType, string crdrType,
            string remarks, string? wlId, bool isCredit, CancellationToken ct)
        {
            // Layer 1: acquire per-user application lock
            var sem = _userLocks.GetOrAdd(userId, _ => new SemaphoreSlim(1, 1));
            await sem.WaitAsync(ct);
            try
            {
                bool hasActiveTx = _context.Database.CurrentTransaction != null;

                if (hasActiveTx)
                {
                    // Caller already owns a transaction — join it so the UPDLOCK
                    // read is held under the same transaction until caller commits.
                    return await ExecuteAsync(userId, userName, txnAmount, amount,
                        surComm, tds, txnType, crdrType, remarks, wlId, isCredit, ct);
                }
                else
                {
                    // Start our own transaction so the UPDLOCK (Layer 2) is held
                    // between the balance read and the insert.
                    using var tx = await _context.Database.BeginTransactionAsync(
                        IsolationLevel.ReadCommitted, ct);
                    try
                    {
                        var result = await ExecuteAsync(userId, userName, txnAmount, amount,
                            surComm, tds, txnType, crdrType, remarks, wlId, isCredit, ct);
                        await tx.CommitAsync(ct);
                        return result;
                    }
                    catch
                    {
                        await tx.RollbackAsync(ct);
                        throw;
                    }
                }
            }
            finally
            {
                sem.Release();
            }
        }

        private async Task<(decimal OldBalance, decimal NewBalance, int EntryId)> ExecuteAsync(
            int userId, string userName, decimal txnAmount, decimal amount,
            decimal surComm, decimal tds, string txnType, string crdrType,
            string remarks, string? wlId, bool isCredit, CancellationToken ct)
        {
            // Read latest balance with UPDLOCK (Layer 2).
            // The lock is held until the enclosing transaction commits, preventing
            // any concurrent session from reading the same "latest" value.
            decimal oldBal = await GetLockedBalanceAsync(userId, ct);
            decimal newBal = isCredit ? oldBal + amount : oldBal - amount;

            var entry = new Tbluserbalance
            {
                UserId    = userId,
                UserName  = userName,
                OldBal    = oldBal,
                Amount    = amount,
                NewBal    = newBal,
                TxnType   = txnType,
                CrdrType  = crdrType,
                Remarks   = remarks,
                WlId      = wlId,
                Txndate   = DateTime.Now,
                TxnAmount = txnAmount,
                SurCom    = surComm,
                Tds       = tds
            };

            _context.Tbluserbalances.Add(entry);
            await _context.SaveChangesAsync(ct);

            _logger.LogInformation(
                "WalletService: UserId={UserId} {CrDr} Amt={Amt:F2} OldBal={Old:F2} NewBal={New:F2} EntryId={Id}",
                userId, crdrType, amount, oldBal, newBal, entry.Id);

            return (oldBal, newBal, entry.Id);
        }

        /// <summary>
        /// Reads the latest balance using SQL WITH (UPDLOCK, ROWLOCK).
        /// The update lock prevents any other transaction from acquiring a shared
        /// or update lock on the same row, effectively serialising concurrent
        /// balance reads for the same user until this transaction commits.
        /// </summary>
        private async Task<decimal> GetLockedBalanceAsync(int userId, CancellationToken ct)
        {
            var rows = await _context.Database
                .SqlQueryRaw<decimal>(
                    "SELECT ISNULL((SELECT TOP 1 NewBal FROM tbluserbalance " +
                    "WITH (UPDLOCK, ROWLOCK) WHERE UserId = {0} ORDER BY Id DESC), 0)",
                    userId)
                .ToListAsync(ct);

            return rows.FirstOrDefault();
        }

        // ── Super-Admin wallet ─────────────────────────────────────────────────

        public async Task<(decimal OldBalance, decimal NewBalance, int EntryId)> SuperAdminCreditAsync(
            int superAdminId, string userName, decimal txnAmount, decimal amount,
            decimal surComm, decimal tds, string txnType, string remarks,
            CancellationToken ct = default)
        {
            var sem = _superAdminLocks.GetOrAdd(superAdminId, _ => new SemaphoreSlim(1, 1));
            await sem.WaitAsync(ct);
            try
            {
                bool hasActiveTx = _context.Database.CurrentTransaction != null;

                if (hasActiveTx)
                    return await ExecuteSuperAdminAsync(superAdminId, userName, txnAmount, amount, surComm, tds, txnType, remarks, ct);

                using var tx = await _context.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
                try
                {
                    var result = await ExecuteSuperAdminAsync(superAdminId, userName, txnAmount, amount, surComm, tds, txnType, remarks, ct);
                    await tx.CommitAsync(ct);
                    return result;
                }
                catch
                {
                    await tx.RollbackAsync(ct);
                    throw;
                }
            }
            finally
            {
                sem.Release();
            }
        }

        public async Task<decimal> SuperAdminGetBalanceAsync(int superAdminId, CancellationToken ct = default) =>
            await _context.TblSuperAdminUserBalances
                .Where(b => b.UserId == superAdminId)
                .OrderByDescending(b => b.Id)
                .Select(b => b.NewBal)
                .FirstOrDefaultAsync(ct) ?? 0m;

        private async Task<(decimal OldBalance, decimal NewBalance, int EntryId)> ExecuteSuperAdminAsync(
            int superAdminId, string userName, decimal txnAmount, decimal amount,
            decimal surComm, decimal tds, string txnType, string remarks, CancellationToken ct)
        {
            var rows = await _context.Database
                .SqlQueryRaw<decimal>(
                    "SELECT ISNULL((SELECT TOP 1 NewBal FROM tblSuperAdminUserBalance " +
                    "WITH (UPDLOCK, ROWLOCK) WHERE UserId = {0} ORDER BY Id DESC), 0)",
                    superAdminId)
                .ToListAsync(ct);

            decimal oldBal = rows.FirstOrDefault();
            decimal newBal = oldBal + amount;

            var entry = new TblSuperAdminUserBalance
            {
                UserId   = superAdminId,
                UserName = userName,
                OldBal   = oldBal,
                Amount   = amount,
                NewBal   = newBal,
                TxnType  = txnType,
                CrdrType = "CR",
                Remarks  = remarks,
                Txndate  = DateTime.Now,
                TxnAmount = txnAmount,
                SurComm  = surComm,
                Tds      = tds
            };

            _context.TblSuperAdminUserBalances.Add(entry);
            await _context.SaveChangesAsync(ct);

            _logger.LogInformation(
                "WalletService SuperAdmin: Id={Id} Credit Amt={Amt:F2} OldBal={Old:F2} NewBal={New:F2} EntryId={EntryId}",
                superAdminId, amount, oldBal, newBal, entry.Id);

            return (oldBal, newBal, entry.Id);
        }
    }
}

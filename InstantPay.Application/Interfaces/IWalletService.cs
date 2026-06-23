namespace InstantPay.Application.Interfaces
{
    /// <summary>
    /// Generic wallet service. ALL credit / debit / refund operations across
    /// the entire application MUST use this service instead of directly querying
    /// tbluserbalance. This is the single file to modify for any future changes
    /// to wallet balance logic.
    ///
    /// Root-cause fix for race condition (TOCTOU — Time-of-Check-Time-of-Use):
    ///   Two concurrent requests for the same user read the same OldBal before
    ///   either one has written its new record, causing the second record to carry
    ///   a stale OldBal and corrupting the running balance.
    /// </summary>
    public interface IWalletService
    {
        /// <summary>
        /// Atomically reads the user's latest balance, adds <paramref name="amount"/>,
        /// inserts a Credit ledger entry, and returns (OldBalance, NewBalance, EntryId).
        /// Thread-safe — only one credit/debit per userId runs at a time.
        /// </summary>
        Task<(decimal OldBalance, decimal NewBalance, int EntryId)> CreditAsync(
            int userId,
            string userName,
            decimal txnAmount,
            decimal amount,
            decimal surComm,
            decimal tds,
            string txnType,
            string remarks,
            string? wlId = null,
            CancellationToken ct = default);

        /// <summary>
        /// Atomically reads the user's latest balance, subtracts <paramref name="amount"/>,
        /// inserts a Debit ledger entry, and returns (OldBalance, NewBalance, EntryId).
        /// Thread-safe — only one credit/debit per userId runs at a time.
        /// </summary>
        Task<(decimal OldBalance, decimal NewBalance, int EntryId)> DebitAsync(
            int userId,
            string userName,
            decimal txnAmount,
            decimal amount,
            decimal surComm,
            decimal tds,
            string txnType,
            string remarks,
            string? wlId = null,
            CancellationToken ct = default);

        /// <summary>
        /// Returns the user's current wallet balance (non-locking read).
        /// Use for display / pre-checks only. For mutations use CreditAsync / DebitAsync.
        /// </summary>
        Task<decimal> GetBalanceAsync(int userId, CancellationToken ct = default);

        /// <summary>
        /// Atomically credits the super-admin balance ledger (tblSuperAdminUserBalance).
        /// Same TOCTOU protection as CreditAsync but targets the super-admin table.
        /// </summary>
        Task<(decimal OldBalance, decimal NewBalance, int EntryId)> SuperAdminCreditAsync(
            int superAdminId,
            string userName,
            decimal txnAmount,
            decimal amount,
            decimal surComm,
            decimal tds,
            string txnType,
            string remarks,
            CancellationToken ct = default);

        /// <summary>
        /// Returns the super-admin's current balance (non-locking read).
        /// </summary>
        Task<decimal> SuperAdminGetBalanceAsync(int superAdminId, CancellationToken ct = default);
    }
}

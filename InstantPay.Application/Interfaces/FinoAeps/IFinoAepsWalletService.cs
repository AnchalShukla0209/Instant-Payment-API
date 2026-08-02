namespace InstantPay.Application.Interfaces.FinoAeps
{
    public interface IFinoAepsWalletService
    {
        Task<decimal> DebitAsync(int userId, decimal amount, decimal txnamount, decimal charge, string txnType, string bankName, string txnId, CancellationToken ct = default);
        Task<decimal> CreditAsync(int userId, decimal amount, string txnType, string bankName, string txnId, bool isFINOBank = false, CancellationToken ct = default);
        Task<decimal> GetLatestWalletBalanceAsync(int userId, CancellationToken ct = default);
    }
}

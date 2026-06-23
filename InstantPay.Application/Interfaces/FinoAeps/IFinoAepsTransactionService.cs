namespace InstantPay.Application.Interfaces.FinoAeps
{
    public interface IFinoAepsTransactionService
    {
        Task<bool> InsertPendingAsync(FinoAepsTxnRecord record, CancellationToken ct = default);
        Task InsertAsync(FinoAepsTxnRecord record, CancellationToken ct = default);
        Task UpdateStatusAsync(string txnId, string status, string? apiRes, string? rrn, CancellationToken ct = default);
        Task UpdateWithCommissionAsync(string txnId, string status, string? apiRes, FinoAepsCommission commission, decimal newBal, string RRN, CancellationToken ct = default);
    }
}

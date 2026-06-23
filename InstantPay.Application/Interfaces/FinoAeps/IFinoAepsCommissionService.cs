namespace InstantPay.Application.Interfaces.FinoAeps
{
    public interface IFinoAepsCommissionService
    {
        Task<FinoAepsCommission> CalculateCommissionAsync(int userId, decimal amount, string txnType, CancellationToken ct = default);
    }

    public class FinoAepsCommission
    {
        public decimal RetailerCommission { get; set; }
        public decimal MdCommission { get; set; }
        public decimal AdCommission { get; set; }
        public decimal WlCommission { get; set; }
        public decimal Tds { get; set; }
        public decimal Cost { get; set; }
        public int SlabId { get; set; }
    }
}

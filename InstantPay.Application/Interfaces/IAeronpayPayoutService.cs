namespace InstantPay.Application.Interfaces
{
    public interface IAeronpayPayoutService
    {
        Task<AeronpayPayoutResponse> ProcessPayoutAsync(AeronpayPayoutRequest request);
    }

    public class AeronpayPayoutRequest
    {
        public string AccountNumber { get; set; }
        public decimal Amount { get; set; }
        public string ClientReferenceId { get; set; }
        public string Latitude { get; set; }
        public string Longitude { get; set; }
        public string BankAccount { get; set; }
        public string Ifsc { get; set; }
        public string BeneName { get; set; }
        public string BeneEmail { get; set; }
        public string BenePhone { get; set; }
        public string BeneAddress { get; set; }
    }

    public class AeronpayPayoutResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public string? TransactionId { get; set; }
        public string? ReferenceId { get; set; }
        public string? Status { get; set; }
        public string? RawResponse { get; set; }
    }
}

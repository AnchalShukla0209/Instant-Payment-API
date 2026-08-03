namespace InstantPay.Application.Interfaces.FinoAeps
{
    public interface IFinoAepsApiClient
    {
        string ProdIPAddress { get; }

        Task<FinoApiCallResult> PostProdAsync(string url, string bodyJson, CancellationToken ct = default);
        Task<FinoApiCallResult> PostAadharPayProdAsync(string bodyJson, CancellationToken ct = default);
        Task<FinoApiCallResult> PostUatAsync(string bodyJson, CancellationToken ct = default);
        Task<FinoApiCallResult> PostMerchantEkycAsync(string bodyJson, CancellationToken ct = default);
        Task<FinoApiCallResult> PostTransactionEnquiryAsync(string url, string bodyJson, CancellationToken ct = default);

        string ComputeChecksum(string raw);
        string GenerateTxnId();
        string GetMacAddress();
        string DecryptSessionKey(string sessionKeyBase64);
    }
}

namespace InstantPay.SharedKernel.RequestPayload.FinoAEPS
{
    public class FinoAepsDailyLoginCheckRequest
    {
        public string SessionKey { get; set; } = string.Empty;
        public string APIKey { get; set; } = string.Empty;
    }
}

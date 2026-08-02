namespace InstantPay.SharedKernel.Entity.AeronpayConfigDTO
{
    public class AeronpayConfig
    {
        public string OriginalHost { get; set; }
        public string PayoutPath { get; set; }
        public string ClientId { get; set; }
        public string ClientSecret { get; set; }
        public string SenderAccountNumber { get; set; }
        public string StatusCheckUrl { get; set; }
        public string RegisteredMobile { get; set; }
    }
}

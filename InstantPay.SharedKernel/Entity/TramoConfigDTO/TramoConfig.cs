namespace InstantPay.SharedKernel.Entity.TramoConfigDTO
{
    public class TramoConfig
    {
        public string ApiKey { get; set; }
        public string PayoutUrl { get; set; }
        public string StatusCheckUrl { get; set; }
        public string RemitterMobile { get; set; }
        public string RemitterFirstName { get; set; }
        public string RemitterEmail { get; set; }
        public string Latitude { get; set; }
        public string Longitude { get; set; }
        public string BeneAddress { get; set; }
        public string PaymentType { get; set; }
    }
}

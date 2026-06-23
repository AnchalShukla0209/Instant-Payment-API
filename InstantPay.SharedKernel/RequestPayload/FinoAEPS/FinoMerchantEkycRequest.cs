namespace InstantPay.SharedKernel.RequestPayload.FinoAEPS
{
    public class FinoMerchantEkycRequest
    {
        public string SessionKey { get; set; } = string.Empty;
        public string APIKey { get; set; } = string.Empty;
        public string aadharno { get; set; } = string.Empty;
        public string NameasperPan { get; set; } = string.Empty;
        public string mobileno { get; set; } = string.Empty;
        public string DOB { get; set; } = string.Empty;
        public string Pancardno { get; set; } = string.Empty;
        public string Firstname { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string fingerdata { get; set; } = string.Empty;
        public string deviceType { get; set; } = string.Empty;
    }
}

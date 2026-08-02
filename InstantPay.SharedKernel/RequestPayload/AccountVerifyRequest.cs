namespace InstantPay.SharedKernel.RequestPayload
{
    public class AccountVerifyRequest
    {
        public string? UserId { get; set; }
        public string? SenderMobile { get; set; }
        public string? BeneName { get; set; }
        public string? AccountNo { get; set; }
        public string? IfscCode { get; set; }
        public string? BankName { get; set; }
    }
}

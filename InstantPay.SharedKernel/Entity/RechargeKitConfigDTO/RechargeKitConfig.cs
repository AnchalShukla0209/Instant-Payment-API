namespace InstantPay.SharedKernel.Entity.RechargeKitConfigDTO
{
    public class RechargeKitConfig
    {
        public string PayoutUrl { get; set; }
        public string StatusCheckUrl { get; set; }
        public string OperatorFetchUrl { get; set; }
        public string CreditCardBillPaymentUrl { get; set; }
        public string BearerToken { get; set; }
        public string TransferType { get; set; }
    }
}

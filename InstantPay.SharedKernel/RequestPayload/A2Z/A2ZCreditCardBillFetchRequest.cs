namespace InstantPay.SharedKernel.RequestPayload.A2Z
{
    public class A2ZCreditCardBillFetchRequest
    {
        public int provider { get; set; }
        public string number { get; set; } = string.Empty;
        public string customerMobileNumber { get; set; } = string.Empty;
    }
}

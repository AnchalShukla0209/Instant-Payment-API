namespace InstantPay.SharedKernel.RequestPayload.MoneyTransfer.RechargeKit
{
    public class CreditCardBillPaymentRequest
    {
        public string? UserId { get; set; }
        public string? TransactionPin { get; set; }
        public string? MobileNo { get; set; }
        public string? AccountNo { get; set; }
        public string? IFSC { get; set; }
        public string? BankName { get; set; }
        public string? BeneficiaryName { get; set; }
        public decimal? Amount { get; set; }
        public string? OperatorCode { get; set; }
        public string? ComingFrom { get; set; }
    }
}

namespace InstantPay.SharedKernel.RequestPayload.FinoAEPS
{
    public class FinoAepsRequest
    {
        public string? SessionKey { get; set; }
        public string? APIKey { get; set; }
        public string? aadharno { get; set; }
        public string? bankiinno { get; set; }
        public string? mobileno { get; set; }
        public string? customermobileno { get; set; }
        public string? amount { get; set; }
        public string? txntype { get; set; }
        public string? BankName { get; set; }
        public string? latitude { get; set; }
        public string? longitude { get; set; }
        public string? fingerdata { get; set; }
        public string? DeviceSrNo { get; set; }
        public string? deviceType { get; set; }
        public string? comingFrom { get; set; }

        // NPCI step-up OTP & merchant auth fields
        public string? merAuthTxnId { get; set; }
        public string? npciTxnId { get; set; }
        public string? npciTxnRefNo { get; set; }
        public string? npciOtpFor { get; set; }
    }

    public class FinoAepsTransactionStatusRequest
    {
        public string? userid { get; set; }
        public string? APIKey { get; set; }
        public string? ClientRefID { get; set; }
    }
}

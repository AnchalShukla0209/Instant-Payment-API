using Newtonsoft.Json;

namespace InstantPay.SharedKernel.Results
{
    public class FinoAepsResponse
    {
        [JsonProperty("Status_Code")]
        public string? Status_Code { get; set; }

        [JsonProperty("Message")]
        public string? Message { get; set; }

        [JsonProperty("Data")]
        public object? Data { get; set; }
    }

    public class FinoBalanceEnquiryData
    {
        [JsonProperty("AdhaarNo")]
        public string? AdhaarNo { get; set; }
        [JsonProperty("BankName")]
        public string? BankName { get; set; }
        [JsonProperty("UTRNO")]
        public string? UTRNO { get; set; }
        [JsonProperty("Status")]
        public string? Status { get; set; }
        [JsonProperty("CustomerMobile")]
        public string? CustomerMobile { get; set; }
        [JsonProperty("Amount")]
        public string? Amount { get; set; }
        [JsonProperty("AvailableBalance")]
        public string? AvailableBalance { get; set; }
        [JsonProperty("TxnDate")]
        public string? TxnDate { get; set; }
    }

    public class FinoMiniStatementData
    {
        [JsonProperty("AdhaarNo")]
        public string? AdhaarNo { get; set; }
        [JsonProperty("NpciTranData")]
        public string? NpciTranData { get; set; }
        [JsonProperty("TransactionList")]
        public List<FinoTransactionItem>? TransactionList { get; set; }
        [JsonProperty("UTRNO")]
        public string? UTRNO { get; set; }
        [JsonProperty("AvailableBalance")]
        public string? AvailableBalance { get; set; }
        [JsonProperty("TxnDate")]
        public string? TxnDate { get; set; }
    }

    public class FinoTransactionItem
    {
        [JsonProperty("Date")]
        public string? Date { get; set; }
        [JsonProperty("ModeOfTxn")]
        public string? ModeOfTxn { get; set; }
        [JsonProperty("Type")]
        public string? Type { get; set; }
        [JsonProperty("RefNo")]
        public string? RefNo { get; set; }
        [JsonProperty("DebitCredit")]
        public string? DebitCredit { get; set; }
        [JsonProperty("Amount")]
        public decimal Amount { get; set; }
    }

    public class FinoOtherData
    {
        [JsonProperty("Status")]
        public string? Status { get; set; }
        [JsonProperty("TxnDate")]
        public string? TxnDate { get; set; }
    }
}

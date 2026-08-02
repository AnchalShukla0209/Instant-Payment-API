using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace InstantPay.SharedKernel.Results.A2Z
{
    public class A2ZCreditCardBillFetchResponse
    {
        public int status { get; set; }
        public string message { get; set; } = string.Empty;
        public A2ZCreditCardBillInfo billInfo { get; set; } = new A2ZCreditCardBillInfo();
        public string? serviceName { get; set; }
        public int isAmountEditable { get; set; }
        public string is_allowed_to_pay_gt_bill_amount { get; set; } = string.Empty;
    }

    public class A2ZCreditCardBillInfo
    {
        public decimal Billamount { get; set; }
        public string billDate { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public string Duedate { get; set; } = string.Empty;
        public string context { get; set; } = string.Empty;
        public List<object> aditionalInfomation { get; set; } = new List<object>();
        public string minBillPaidAmount { get; set; } = string.Empty;
        public string maxBillPaidAmount { get; set; } = string.Empty;
        public string maxAllowPaidAmount { get; set; } = string.Empty;

        [JsonPropertyName("Minimum Amount Due")]
        public string MinimumAmountDue { get; set; } = string.Empty;

        [JsonPropertyName("Current Outstanding Amount")]
        public string CurrentOutstandingAmount { get; set; } = string.Empty;
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InstantPay.SharedKernel.Results.MoneyTransfer.NIFI
{
    public class NifiEncryptedResponse
    {
        public string body { get; set; }
    }

    public class NifiPayoutResponse
    {
        public int statuscode { get; set; }
        public string status { get; set; }
        public string message { get; set; }
        public string uniqueId { get; set; }
        public string apiTxnId { get; set; }
        public NifiPayoutData data { get; set; }
        public NifiDeduction deduction { get; set; }
        public string timestamp { get; set; }
        public string environment { get; set; }
    }

    public class NifiPayoutData
    {
        public string bank_ref_num { get; set; }
        public string externalRef { get; set; }
        public string account { get; set; }
        public string ifsc { get; set; }
        public string recipient_name { get; set; }
    }

    public class NifiDeduction
    {
        public string amount { get; set; }
        public string charges { get; set; }
        public string tax { get; set; }
        public string adjustment { get; set; }
        public string balance { get; set; }
    }

    public class NifiStatusResponse
    {
        public int statuscode { get; set; }
        public string status { get; set; }
        public string message { get; set; }
        public string uniqueId { get; set; }
        public string ApiTxnId { get; set; }
        public NifiStatusData data { get; set; }
        public NifiDeduction deduction { get; set; }
        public string timestamp { get; set; }
        public string environment { get; set; }
    }

    public class NifiStatusData
    {
        public decimal amount { get; set; }
        public string BankRefNo { get; set; }
        public string Account { get; set; }
        public string ifsc { get; set; }
        public string Name { get; set; }
        public string Mobile { get; set; }
        public string Email { get; set; }
        public string Bank { get; set; }
        public string PaymentMode { get; set; }
        public string TxnId { get; set; }
        public DateTime TransactionDate { get; set; }
    }

    public class NifiWebhookData
    {
        public string statuscode { get; set; }
        public string status { get; set; }
        public string message { get; set; }
        public string uniqueId { get; set; }
        public string ApiTxnId { get; set; }
        public NifiStatusData data { get; set; }
        public NifiDeduction deduction { get; set; }
        public string timestamp { get; set; }
        public string environment { get; set; }
    }

    public class NifiErrorResponse
    {
        public string message { get; set; }
    }
}

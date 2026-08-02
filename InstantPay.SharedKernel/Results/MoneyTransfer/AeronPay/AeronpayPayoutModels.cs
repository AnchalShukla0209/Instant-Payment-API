namespace InstantPay.SharedKernel.Results.MoneyTransfer.AeronPay
{
    // ─── Payout API response from AeronPay ────────────────────────────────────
    // status: "SUCCESS" | "PENDING" | "FAILED" | "BAD_REQUEST_ERROR"

    public class AeronpayPayoutApiResponse
    {
        public string status { get; set; }
        public string statusCode { get; set; }
        public string message { get; set; }
        public AeronpayPayoutData data { get; set; }
    }

    public class AeronpayPayoutData
    {
        public string transactionId { get; set; }
        public string utr { get; set; }
        public string client_referenceId { get; set; }
        public int acknowledged { get; set; }
    }

    // ─── Status check API request to AeronPay ─────────────────────────────────

    public class AeronpayStatusCheckRequest
    {
        public string client_referenceId { get; set; }
        public string mobile { get; set; }
        public string date_of_transaction { get; set; }
    }

    // ─── Status check API response from AeronPay ──────────────────────────────
    // statusCode: "200" SUCCESS | "201" PENDING | "202" ACCEPTED | "400" FAILED
    //             "404" not found | "429" rate limited | "444" temp issue

    public class AeronpayStatusCheckResponse
    {
        public string status { get; set; }
        public string statusCode { get; set; }
        public string transactionId { get; set; }
        public string enquiryTxnId { get; set; }
        public string client_referenceId { get; set; }
        public string acknowledged { get; set; }
        public decimal? amount { get; set; }
        public string utr { get; set; }
        public string description { get; set; }
        public string message { get; set; }
    }

    // ─── Webhook payload received from AeronPay ───────────────────────────────

    public class AeronpayWebhookPayload
    {
        public string? status { get; set; } = "";
        public string? statusCode { get; set; } = "";
        public string? transactionId { get; set; } = "";
        public string? client_referenceId { get; set; }= "";
        public decimal? txn_amount { get; set; } = 0;
        public string? acknowledged { get; set; } = "";
        public string? utr { get; set; } = "";
        public string? description { get; set; } = "";
    }
}

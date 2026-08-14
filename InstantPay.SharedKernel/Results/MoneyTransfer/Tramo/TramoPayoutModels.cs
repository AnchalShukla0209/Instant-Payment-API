using Newtonsoft.Json;

namespace InstantPay.SharedKernel.Results.MoneyTransfer.Tramo
{
    // ─── Payout API response from Tramo ───────────────────────────────────────
    // code: 200 = success  |  400 = error  |  500 = internal server error
    // data.status: "Success" | "Failed" | "Hold" | "Pending" | "in_process" | "Initiated" | "Queued"

    public class TramoOperatorKeys
    {
        public string? key1 { get; set; }
        public string? key2 { get; set; }
        public string? key3 { get; set; }
    }

    public class TramoPayoutData
    {
        public string? clientRefId { get; set; }
        public string? transactionType { get; set; }
        public string? productName { get; set; }
        public string? categoryName { get; set; }
        public decimal amount { get; set; }
        public decimal credit { get; set; }
        public decimal debit { get; set; }
        public decimal TDS { get; set; }
        public decimal GST { get; set; }
        public string? status { get; set; }
        public string? vendorUtrNumber { get; set; }

        [JsonProperty("operator")]
        public TramoOperatorKeys? operatorKeys { get; set; }

        public string? mobileNumber { get; set; }
        public string? createdAt { get; set; }
        public string? modeOfPayment { get; set; }
        public string? partnerTransactionId { get; set; }
    }

    public class TramoPayoutApiResponse
    {
        public int code { get; set; }
        public TramoPayoutData? data { get; set; }
        public string? message { get; set; }
    }

    // ─── Status check API response from Tramo ─────────────────────────────────

    public class TramoStatusCheckResponse
    {
        public int code { get; set; }
        public TramoPayoutData? data { get; set; }
        public string? message { get; set; }
    }

    // ─── Webhook (callback) payload received from Tramo ─────────────────────────
    // partnerTransactionId: our refId (TxnId)
    // status: "Success" | "Failed" | "Hold" | "Pending" | "in_process" | "Initiated" | "Queued"
    // Tramo posts: { clientRefId, partnerTransactionId, status, utr, remarks, message }

    public class TramoWebhookPayload
    {
        public string? clientRefId { get; set; }
        public string? partnerTransactionId { get; set; }
        public string? status { get; set; }
        public string? utr { get; set; }
        public string? remarks { get; set; }
        public string? message { get; set; }
    }

    // ─── Check-status request body sent to Tramo ──────────────────────────────

    public class TramoCheckStatusRequest
    {
        public string clientRefId { get; set; }
        public string partnerTransactionId { get; set; }
    }
}

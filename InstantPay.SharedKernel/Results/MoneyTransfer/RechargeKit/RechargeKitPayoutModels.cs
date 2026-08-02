namespace InstantPay.SharedKernel.Results.MoneyTransfer.RechargeKit
{
    // ─── Payout API response from RechargeKit ──────────────────────────────────
    // error: 0 = processed  | non-zero = not processed
    // status: 1=SUCCESS  2=PENDING  3=FAILURE  others=HOLD

    public class RechargeKitPayoutApiResponse
    {
        public int error { get; set; }
        public string msg { get; set; }
        public int status { get; set; }
        public string orderid { get; set; }
        public string optransid { get; set; }
        public string partnerreqid { get; set; }
        public string user_var1 { get; set; }
        public string user_var2 { get; set; }
        public string user_var3 { get; set; }
    }

    // ─── Status check API response from RechargeKit ────────────────────────────

    public class RechargeKitStatusCheckResponse
    {
        public int error { get; set; }
        public string msg { get; set; }
        public int status { get; set; }
        public string orderid { get; set; }
        public string optransid { get; set; }
        public decimal amount { get; set; }
        public decimal commission { get; set; }
    }

    // ─── Webhook payload received from RechargeKit ────────────────────────────
    // status: 1=SUCCESS  3=FAILURE
    // pid: our partner_request_id (TxnId)

    public class RechargeKitWebhookPayload
    {
        public int status { get; set; }
        public string? orderid { get; set; } = "";
        public string? opttranid { get; set; } = "";
        public string? pid { get; set; } = "";
        public decimal commission { get; set; }
    }

    // ─── Service-wise operator list response from RechargeKit ─────────────────

    public class RechargeKitOperatorResponse
    {
        public int error { get; set; }
        public string? msg { get; set; }
        public int status { get; set; }
        public List<RechargeKitOperator> operatorList { get; set; } = new List<RechargeKitOperator>();
    }

    public class RechargeKitOperator
    {
        public int operator_id { get; set; }
        public string? operator_name { get; set; }
        public string? service_name { get; set; }
        public int operator_category { get; set; }
        public string? state_name { get; set; }
        public string? operator_ifsc { get; set; }
        public string? operator_customer_params { get; set; }
        public string? operator_additional_info { get; set; }
        public string? operator_category_name { get; set; }
        public int providerid { get; set; }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InstantPay.SharedKernel.Results.MoneyTransfer.Finzep
{
    // ─── Outbound payout request to Finzep ────────────────────────────────────

    public class FinzepPayoutApiRequest
    {
        public int UserID { get; set; }
        public string Token { get; set; }
        public int OutletID { get; set; }
        public FinzepPayoutInner PayoutRequest { get; set; }
    }

    public class FinzepPayoutInner
    {
        public string AccountNo { get; set; }
        public decimal AmountR { get; set; }
        public int BankID { get; set; }
        public string IFSC { get; set; }
        public string SenderMobile { get; set; }
        public string SenderName { get; set; }
        public string SenderEmail { get; set; }
        public string BeneName { get; set; }
        public string BeneMobile { get; set; }
        public int APIRequestID { get; set; }
        public string SPKey { get; set; }
        public string WebHook { get; set; }
    }

    // ─── Payout API response from Finzep ──────────────────────────────────────
    // status: 2=Success, 1=Pending, 3=Failed, 4=Refunded
    // rpid = TxnId / ApiTxnId
    // liveID = BankRefNo (brid)

    public class FinzepPayoutApiResponse
    {
        public int status { get; set; }
        public string beneName { get; set; }
        public string rpid { get; set; }
        public string liveID { get; set; }
        public int statuscode { get; set; }
        public string message { get; set; }
        public int errorCode { get; set; }
        public decimal opening { get; set; }
        public decimal closing { get; set; }
        public decimal chargedAmount { get; set; }
        public decimal trxAmount { get; set; }
    }

    // ─── Status Check API response from Finzep ────────────────────────────────

    public class FinzepStatusApiResponse
    {
        public int status { get; set; }
        public string msg { get; set; }
        public decimal bal { get; set; }
        public string errorcode { get; set; }
        public string account { get; set; }
        public decimal amount { get; set; }
        public string rpid { get; set; }
        public string agentid { get; set; }
        public string opid { get; set; }
    }

    // ─── Webhook payload received from Finzep ─────────────────────────────────

    public class FinzepWebhookPayload
    {
        public int status { get; set; }
        public string msg { get; set; }
        public string rpid { get; set; }
        public string liveID { get; set; }
        public string beneName { get; set; }
        public string account { get; set; }
        public decimal amount { get; set; }
        public string message { get; set; }
        public int errorCode { get; set; }
        public decimal chargedAmount { get; set; }
        public string opid { get; set; }
    }
}

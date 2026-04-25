using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InstantPay.SharedKernel.RequestPayload.RazorPay
{
    public class RazorpayWebhookRequest
    {
        public string Event { get; set; }
        public RazorpayPayload Payload { get; set; }
    }

    public class RazorpayPayload
    {
        public RazorpayPaymentWrapper Payment { get; set; }
    }
    public class RazorpayPaymentWrapper
    {
        public RazorpayPaymentEntity Entity { get; set; }
    }
    public class RazorpayPaymentEntity
    {
        public string Id { get; set; }
        public string Order_Id { get; set; }
        public string Status { get; set; }
        public string Method { get; set; }

        public RazorpayCard Card { get; set; }
        public RazorpayAcquirerData Acquirer_Data { get; set; } // ✅ RRN here
    }

    public class RazorpayCard
    {
        public string Network { get; set; }
        public string Last4 { get; set; }
    }

    public class RazorpayAcquirerData
    {
        public string Rrn { get; set; }
        public string Auth_Code { get; set; }
    }

}

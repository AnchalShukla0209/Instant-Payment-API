using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InstantPay.SharedKernel.RequestPayload.WhatsApp
{
    public class WhatsAppBroadcastRequest
    {
        public string Message { get; set; }
        public bool? SendToActiveUsersOnly { get; set; } = true;
    }

    public class WhatsAppBroadcastResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public int TotalUsers { get; set; }
        public int SuccessfulSends { get; set; }
        public int FailedSends { get; set; }
        public List<string> FailedPhoneNumbers { get; set; } = new();
        public DateTime SentAt { get; set; }
    }
}

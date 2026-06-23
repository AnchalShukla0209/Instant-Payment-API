using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InstantPay.SharedKernel.Results.MoneyTransfer.NIFI
{
    public class NifiPayoutRequest
    {
        public string p1 { get; set; }  // Account Number
        public string p2 { get; set; }  // IFSC
        public string p3 { get; set; }  // API Txn ID
        public string p4 { get; set; }  // Amount
        public string p5 { get; set; }  // Beneficiary Name
        public string p6 { get; set; }  // Mobile
        public string p7 { get; set; }  // Email
        public string p8 { get; set; }  // Sender Name
        public string p9 { get; set; }  // Transaction Type (Payout)
        public string p10 { get; set; } // Channel (1)
        public string p11 { get; set; } // Lat,Long
        public string p72 { get; set; } // IP Address
        public string p73 { get; set; } // Sender Mobile
        public string p74 { get; set; } // Is Verify (false)
    }

    public class NifiEncryptedRequest
    {
        public string body { get; set; }
    }
}

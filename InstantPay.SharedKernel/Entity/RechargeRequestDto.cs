using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InstantPay.SharedKernel.Entity
{

    public class EncryptedWrapperDto
    {
        public RechargeRequestDto payload { get; set; }
    }

    public class RechargeRequestDto
    {
        public int UserId { get; set; }
        public string userName { get; set; }
        public string MobileNumber { get; set; } = string.Empty;
        public string Operator { get; set; } = string.Empty;
        public string operatorCode { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string TxnPin { get; set; } = string.Empty;
        public string Type { get; set; } = "REC";
        public string CustomerRefNo { get; set; } = string.Empty;
    }

}

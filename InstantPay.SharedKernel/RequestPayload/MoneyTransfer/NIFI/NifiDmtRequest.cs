using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InstantPay.SharedKernel.RequestPayload.MoneyTransfer.NIFI
{
    public class NifiDmtRequest
    {
        public string? UserId { get; set; }
        public string? TransactionPin { get; set; }
        public decimal? Amount { get; set; }
        public string? AccountNumber { get; set; }
        public string? BeneficiaryName { get; set; }
        public string? BankName { get; set; }
        public string? IFSC { get; set; }
        public string? Mobile { get; set; }
        public string? Email { get; set; }
        public string? Remark { get; set; }
    }
}

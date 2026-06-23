using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InstantPay.SharedKernel.RequestPayload.MoneyTransfer.Finzep
{
    public class FinzepDmtRequest
    {
        public string? UserId { get; set; }
        public string? TransactionPin { get; set; }
        public decimal? Amount { get; set; }
        public string? AccountNumber { get; set; }
        public string? BeneficiaryName { get; set; }
        public string? BeneficiaryMobile { get; set; }
        public string? BankName { get; set; }
        public string? IFSC { get; set; }
        public string? Remark { get; set; }
        public string? ComingFrom { get; set; }
    }
}

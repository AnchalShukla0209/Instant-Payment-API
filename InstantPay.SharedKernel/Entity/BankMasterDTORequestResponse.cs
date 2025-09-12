using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InstantPay.SharedKernel.Entity
{
    public class BankDto
    {
        public Guid BankId { get; set; }
        public string BankName { get; set; }
        public string AccountNumber { get; set; }
        public string IFSCCode { get; set; }
        public string PhoneNo { get; set; }
        public decimal? TxnCharge { get; set; }
        public bool IsActive { get; set; }
    }

    public class CreateBankCommand : IRequest<Guid>
    {
        public string BankName { get; set; }
        public string AccountNumber { get; set; }
        public string IFSCCode { get; set; }
        public string PhoneNo { get; set; }
        public decimal TxnCharge { get; set; }
        public int CreatedBy { get; set; }
    }

    public class UpdateBankCommand : IRequest
    {
        public Guid BankId { get; set; }
        public string BankName { get; set; }
        public string AccountNumber { get; set; }
        public string IFSCCode { get; set; }
        public string PhoneNo { get; set; }
        public decimal TxnCharge { get; set; }
        public int ModifiedBy { get; set; }
    }

}

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InstantPay.Infrastructure.Sql.Entities
{
    
    public partial class JPBBankMaster
    {
        public int Id { get; set; }
        public string IssuerBankName { get; set; }
        public string BankCode { get; set; }
        public string IIN { get; set; }
        public bool IsActive { get; set; }
    }
}

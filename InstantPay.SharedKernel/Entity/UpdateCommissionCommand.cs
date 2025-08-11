using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InstantPay.SharedKernel.Entity
{
    public class UpdateCommissionCommand 
    {
        public int Id { get; set; }
        public decimal? IPShare { get; set; }
        public decimal? WLShare { get; set; }
        public string CommissionType { get; set; }
    }

    public class UpdateCommissionResult
    {
        public string ErrorMsg { get; set; }
        public bool Flag { get; set; }
    }
}

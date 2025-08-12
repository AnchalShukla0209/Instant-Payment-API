using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InstantPay.SharedKernel.Entity
{
    public class ServiceRightsData
    {
        public string microatm { get; set; }
        public string moneytransfer { get; set; }
        public string billpayment { get; set; }
        public string recharge { get; set; }
        public string aeps { get; set; }
    }
}

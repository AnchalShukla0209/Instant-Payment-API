using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InstantPay.SharedKernel.Results
{
    public class InsuranceFetchResponseDto
    {
        public int Status { get; set; }
        public string Operator { get; set; }
        public string Mobile { get; set; }
        public List<InsuranceData> Rdata { get; set; }
    }

    public class InsuranceData
    {
        public int? Status { get; set; }
        public string Desc { get; set; }
        public string CustomerName { get; set; }
        public string Netamount { get; set; }
        public string Duedatefromto { get; set; }
    }
}

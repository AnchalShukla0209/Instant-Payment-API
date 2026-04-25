using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InstantPay.SharedKernel.Results
{
    public class BillFetchResponseDto
    {
        public int Status { get; set; }
        public string Mobile { get; set; }
        public string Operator { get; set; }
        public List<BillData> Rdata { get; set; }
    }

    public class BillData
    {
        public int? Status { get; set; }
        public string Desc { get; set; }
        public string CustomerName { get; set; }
        public string Billamount { get; set; }
        public string Billdate { get; set; }
        public string Duedate { get; set; }
        public string BillNumber { get; set; }
    }
}

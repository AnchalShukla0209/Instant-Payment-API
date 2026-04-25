using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InstantPay.SharedKernel.RequestPayload
{
    public class BillFetchRequestDto
    {
        public string Mobile { get; set; }
        public string Operator { get; set; }
        public string Type { get; set; } = "ebill";
        public string optional { get; set; } = "";
    }
}

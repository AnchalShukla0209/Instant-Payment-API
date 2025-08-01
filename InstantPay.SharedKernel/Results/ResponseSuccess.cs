using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InstantPay.SharedKernel.Results
{
    public class ResponseSuccess
    {
        public bool? success { get; set; }
        public string? message { get; set; }
        public string? txnid { get; set; }
        public string? apitxnid { get; set; }
        public string? transactiondatetime { get; set; }
    }
}

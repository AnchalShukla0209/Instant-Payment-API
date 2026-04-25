using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InstantPay.SharedKernel.RequestPayload.A2Z
{
    public class A2ZRechargePlanRequest
    {
        public string mobile_number { get; set; }
        public string provider { get; set; }
    }
}

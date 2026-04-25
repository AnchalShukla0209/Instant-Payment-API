using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InstantPay.SharedKernel.Results.A2Z
{
    public class A2ZRechargePlanResponse
    {
        public int status { get; set; }
        public string message { get; set; }
        public object data { get; set; }
    }
}

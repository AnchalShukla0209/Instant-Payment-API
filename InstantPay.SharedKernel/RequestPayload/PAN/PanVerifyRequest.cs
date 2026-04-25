 using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InstantPay.SharedKernel.RequestPayload.PAN
{
    public class PanVerifyRequest
    {
        public string PanNumber { get; set; } = string.Empty;
    }
}

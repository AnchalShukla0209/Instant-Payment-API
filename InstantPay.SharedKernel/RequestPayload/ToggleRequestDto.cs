using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InstantPay.SharedKernel.RequestPayload
{
    public class ToggleRequestDto
    {
        public string ServiceCode { get; set; }    // AEPS, DMT, BILLPAY
        public string? ProviderCode { get; set; } = "";  // FINO, JPB (optional)
        public string? FeatureCode { get; set; } = "";   // WITHDRAW, ELECTRICITY
        public bool IsEnabled { get; set; }        // true / false
    }

}

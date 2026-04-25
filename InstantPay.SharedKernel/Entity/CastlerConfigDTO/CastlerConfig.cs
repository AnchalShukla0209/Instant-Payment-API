using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InstantPay.SharedKernel.Entity.CastlerConfigDTO
{
    public class CastlerConfig
    {
        public string? BaseUrl { get; set; }
        public string? ApiAccNo { get; set; }
        public string? XApiKey { get; set; }
        public string? ApiKey { get; set; }
        public string? ApiSecret { get; set; }
        public int? MonthlyLimit { get; set; }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace InstantPay.SharedKernel.Results
{
    public class RechargePlanResponse
    {
        public string tel { get; set; }
        [JsonPropertyName("operator")]
        public string OperatorName { get; set; }
        public List<RechargePlanRecord> records { get; set; }
        public int status { get; set; }
        public double time { get; set; }
    }

    public class RechargePlanRecord
    {
        [JsonPropertyName("rs")]
        [JsonConverter(typeof(FlexibleStringConverter))]
        public string rs { get; set; }
        public string desc { get; set; }
    }

}

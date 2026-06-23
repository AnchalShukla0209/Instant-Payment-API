using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InstantPay.SharedKernel.Results
{
    public class FeatureDto
    {
        public string Key { get; set; }
        public string? ProviderCode { get; set; }
        public string Label { get; set; }
        public string Icon { get; set; }
        public string Config { get; set; }
        public bool? isEnabled { get; set; }
    }

    public class ServiceDTO
    {
        public string key { get; set; }
        public string label { get; set; }
        public string icon { get; set; }
        public bool? isActiveOnWeb{ get; set; }
        public bool? isActiveOnApk { get; set; }

    }

    public class ProviderDTO
    {
        public string key { get; set; }
        public string label { get; set; }
        public bool? isEnabled { get; set; }
    }

}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InstantPay.SharedKernel.Entity.FinzepConfigDTO
{
    public class FinzepConfig
    {
        public int UserID { get; set; }
        public string? Token { get; set; }
        public int OutletID { get; set; }
        public string? SenderMobile { get; set; }
        public string? SenderName { get; set; }
        public string? SenderEmail { get; set; }
        public string? AgentID { get; set; }
        public string? WebhookUrl { get; set; }
    }
}

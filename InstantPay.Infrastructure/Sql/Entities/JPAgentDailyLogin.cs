using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InstantPay.Infrastructure.Sql.Entities
{
    public partial class JPAgentDailyLogin
    {
        public int Id { get; set; }
        public string AgentLoginId { get; set; }
        public string Mobile { get; set; }
        public string AadharNumber { get; set; }
        public string AgentPinCode { get; set; }
        public string AgentRefNo { get; set; }
        [Column(TypeName = "decimal(18,12)")]
        public double Lattitude { get; set; }
        [Column(TypeName = "decimal(18,12)")]
        public double Longtitude { get; set; }
        public bool Status { get; set; }
        public DateTime LoginDateTime { get; set; }
    }
}

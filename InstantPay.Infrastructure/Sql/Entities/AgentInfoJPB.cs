using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InstantPay.Infrastructure.Sql.Entities
{
    public partial class AgentInfoJPB
    {
        public int Id { get; set; }
        public string ApplicationNumber { get; set; } = string.Empty;
        public string AgentRefNo { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public int? UserId { get; set; }
        public string UserType { get; set; } = string.Empty;
        public bool isActive { get; set; }
        public DateTime CreatedOn { get; set; }
    }
}

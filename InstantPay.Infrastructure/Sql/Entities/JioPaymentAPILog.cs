using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InstantPay.Infrastructure.Sql.Entities
{
    public partial class JioPaymentAPILog
    {
        public int Id { get; set; }
        public string APIURL { get; set; }
        public string Method { get; set; }
        public string SuccessCode { get; set; }
        public string APIError { get; set; }
        public string APIHeaders { get; set; }
        public string APIPayload { get; set; }
        public string APIResponse { get; set; }
        public DateTime CreatedOn { get; set; }
        public bool IsDeleted { get; set; }
        public string Service { get; set; }
        public string Mode { get; set; }
    }
}

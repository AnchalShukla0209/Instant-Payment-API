using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InstantPay.SharedKernel.Entity
{
    public class NotificationDto
    {
        public int? Id { get; set; }
        public string Content { get; set; }
        public string Status { get; set; }  // Active/Inactive
    }
}

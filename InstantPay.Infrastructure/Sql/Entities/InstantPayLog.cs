using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InstantPay.Infrastructure.Sql.Entities
{
    public class InstantPayLog
    {
        [Key]
        public int Id { get; set; }

        public string? Request { get; set; }

        public string? Response { get; set; }

        public string? APIMode { get; set; }

        public DateTime? CreatedOn { get; set; } = DateTime.Now;
    }
}

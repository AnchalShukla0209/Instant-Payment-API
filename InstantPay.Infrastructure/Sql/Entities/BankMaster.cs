using InstantPay.Infrastructure.Sql.Entities;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InstantPay.Infrastructure.Sql.Entities
{
    [Table("BankMaster")]
    public class BankMaster
    {
        [Key]
        public Guid BankId { get; set; }

        [Required]
        [MaxLength(50)]
        public string BankName { get; set; }

        [MaxLength(30)]
        public string AccountNumber { get; set; }

        [MaxLength(30)]
        public string IFSCCode { get; set; }

        [MaxLength(20)]
        public string PhoneNo { get; set; }

        [Column(TypeName = "decimal(16,2)")]
        public decimal? TxnCharge { get; set; }

        public bool IsActive { get; set; } = true;

        public bool IsDeleted { get; set; } = false;

        [Required]
        public int CreatedBy { get; set; }

        [Required]
        public DateTime CreatedOn { get; set; } = DateTime.UtcNow;

        public int? ModifiedBy { get; set; }

        public DateTime? ModifiedOn { get; set; }
    }


}

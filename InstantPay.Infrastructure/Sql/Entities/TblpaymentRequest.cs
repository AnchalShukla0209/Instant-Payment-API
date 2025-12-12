using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace InstantPay.Infrastructure.Sql.Entities;

[Table("TblPaymentRequest")]
public class TblPaymentRequest
{
    [Key]
    public Guid PaymentId { get; set; } = Guid.NewGuid();

    [Required]
    public Guid BankId { get; set; }

    [Required]
    public int UserId { get; set; }

    [Column(TypeName = "decimal(16,2)")]
    public decimal Amount { get; set; }

    [Required, MaxLength(20)]
    public string TxnId { get; set; }

    [Required, MaxLength(10)]
    public string DeposideMode { get; set; }

    [MaxLength(50)]
    public string TxnSlipFileName { get; set; }

    [MaxLength(500)]
    public string TxnSlipPath { get; set; }

    [MaxLength(20)]
    public string Status { get; set; }

    [MaxLength(250)]
    public string? AdminRemarks { get; set; }

    public int CreatedBy { get; set; }
    public DateTime? CreatedOn { get; set; } 
    public int? ModifiedBy { get; set; }
    public DateTime? ModifiedOn { get; set; }
    public bool IsDeleted { get; set; } = false;
}

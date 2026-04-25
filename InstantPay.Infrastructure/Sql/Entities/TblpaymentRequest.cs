using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace InstantPay.Infrastructure.Sql.Entities;

[Table("TblPaymentRequestNew")]
public class TblPaymentRequest
{
    [Key]
    public Guid? PaymentId { get; set; } = Guid.NewGuid();

    public Guid? BankId { get; set; }

    public int? UserId { get; set; }

    [Column(TypeName = "decimal(16,2)")]
    public decimal? Amount { get; set; }

    public string? TxnId { get; set; }

    public string? PaymentTxnId { get; set; }

    [MaxLength(150)]
    public string? DeposideMode { get; set; }

    [MaxLength(50)]
    public string? TxnSlipFileName { get; set; }

    [MaxLength(500)]
    public string? TxnSlipPath { get; set; }

    [MaxLength(20)]
    public string? Status { get; set; }

    [MaxLength(250)]
    public string? AdminRemarks { get; set; }

    [MaxLength(500)]
    public string? UserRemarks { get; set; }

    public decimal? openingBalance { get; set; }

    public decimal? closingBalance { get; set; }

    public int? CreatedBy { get; set; }

    public DateTime? CreatedOn { get; set; }

    public int? ModifiedBy { get; set; }

    public DateTime? ModifiedOn { get; set; }

    public bool? IsDeleted { get; set; } = false;
}

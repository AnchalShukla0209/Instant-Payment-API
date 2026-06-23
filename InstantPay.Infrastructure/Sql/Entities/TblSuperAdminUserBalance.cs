using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace InstantPay.Infrastructure.Sql.Entities;

public partial class TblSuperAdminUserBalance
{
    public int Id { get; set; }

    public int? UserId { get; set; }

    public string? UserName { get; set; }

    public decimal? OldBal { get; set; }

    public decimal? Amount { get; set; }

    public decimal? NewBal { get; set; }

    public string? TxnType { get; set; }

    public string? CrdrType { get; set; }

    public string? Remarks { get; set; }

    [Column("txndate")]
    public DateTime? Txndate { get; set; }

    public decimal? TxnAmount { get; set; }

    [Column("Sur_Comm")]
    public decimal? SurComm { get; set; }

    public decimal? Tds { get; set; }
}

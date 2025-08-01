using System;
using System.Collections.Generic;

namespace InstantPay.Infrastructure.Sql.Entities;

public partial class TblApiwallet
{
    public int Id { get; set; }

    public string UserId { get; set; } = null!;

    public decimal? OldBal { get; set; }

    public decimal? Amount { get; set; }

    public decimal? NewBal { get; set; }

    public string? TxnType { get; set; }

    public string? Remarks { get; set; }

    public DateTime? TxnDate { get; set; }

    public string? IpAddress { get; set; }

    public string? CrDrType { get; set; }

    public string? PayRefId { get; set; }
}

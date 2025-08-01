using System;
using System.Collections.Generic;

namespace InstantPay.Infrastructure.Sql.Entities;

public partial class TblWlbalance
{
    public int Id { get; set; }

    public string UserId { get; set; } = null!;

    public string? UserName { get; set; }

    public decimal? OldBal { get; set; }

    public decimal? Amount { get; set; }

    public decimal? NewBal { get; set; }

    public string? TxnType { get; set; }

    public string? CrdrType { get; set; }

    public string? Remarks { get; set; }

    public DateTime? Txndate { get; set; }

    public decimal? TxnAmount { get; set; }

    public decimal? SurComm { get; set; }

    public decimal? Tds { get; set; }
}

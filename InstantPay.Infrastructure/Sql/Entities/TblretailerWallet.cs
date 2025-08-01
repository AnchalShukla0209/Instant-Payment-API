using System;
using System.Collections.Generic;

namespace InstantPay.Infrastructure.Sql.Entities;

public partial class TblretailerWallet
{
    public int Id { get; set; }

    public string? UserId { get; set; }

    public string? UserName { get; set; }

    public decimal? OldBal { get; set; }

    public decimal? Amount { get; set; }

    public decimal? NewBal { get; set; }

    public string? Txntype { get; set; }

    public string? CrdrType { get; set; }

    public string? DrCrBy { get; set; }

    public string? Remarks { get; set; }

    public decimal? TxnAmount { get; set; }

    public decimal? SurComm { get; set; }

    public string? TxnId { get; set; }

    public DateTime? Txndate { get; set; }

    public string? Opname { get; set; }

    public string? Accountno { get; set; }
}

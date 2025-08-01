using System;
using System.Collections.Generic;

namespace InstantPay.Infrastructure.Sql.Entities;

public partial class TblpaymentRequest
{
    public int Id { get; set; }

    public string UserId { get; set; } = null!;

    public string? UserName { get; set; }

    public string? BankName { get; set; }

    public string? BankId { get; set; }

    public decimal? Amount { get; set; }

    public decimal? Charge { get; set; }

    public decimal? TransferAmount { get; set; }

    public string? TxnId { get; set; }

    public string? DepositMode { get; set; }

    public string? TxnSlip { get; set; }

    public string? Status { get; set; }

    public DateTime? Reqdate { get; set; }

    public DateTime? Updatedate { get; set; }

    public string? AminRemarks { get; set; }

    public string? Usertype { get; set; }
}

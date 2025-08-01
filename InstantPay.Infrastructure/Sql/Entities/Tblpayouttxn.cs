using System;
using System.Collections.Generic;

namespace InstantPay.Infrastructure.Sql.Entities;

public partial class Tblpayouttxn
{
    public int Id { get; set; }

    public string? UserId { get; set; }

    public string? UserName { get; set; }

    public string? MobileNo { get; set; }

    public string? TxnMode { get; set; }

    public string? BeneName { get; set; }

    public string? AccountNo { get; set; }

    public string? Ifsccode { get; set; }

    public string? BankName { get; set; }

    public decimal? OldBal { get; set; }

    public decimal? Amount { get; set; }

    public decimal? Charge { get; set; }

    public decimal? Gst { get; set; }

    public decimal? TotalCost { get; set; }

    public decimal? NewBal { get; set; }

    public string? Status { get; set; }

    public string? TxnId { get; set; }

    public string? Brid { get; set; }

    public string? ClientRefid { get; set; }

    public DateTime? ReqDate { get; set; }

    public string? ApiRequest { get; set; }

    public string? ApiResponse { get; set; }
}

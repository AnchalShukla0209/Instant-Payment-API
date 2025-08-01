using System;
using System.Collections.Generic;

namespace InstantPay.Infrastructure.Sql.Entities;

public partial class Tblonlinepayment
{
    public int Id { get; set; }

    public string UserKey { get; set; } = null!;

    public decimal? Amount { get; set; }

    public string? OrderId { get; set; }

    public string? TxnId { get; set; }

    public string? Reqlogs { get; set; }

    public DateTime? ReqDate { get; set; }

    public DateTime? ResDate { get; set; }

    public string? Status { get; set; }

    public string? Paymentid { get; set; }

    public string? Gatwaytype { get; set; }

    public string? UserName { get; set; }

    public string? MobileNo { get; set; }

    public string? Pancard { get; set; }

    public string? AadharCard { get; set; }

    public string? PanName { get; set; }

    public string? WlId { get; set; }

    public string? Mdid { get; set; }

    public string? AdId { get; set; }

    public decimal? TxnCharge { get; set; }

    public decimal? Gst { get; set; }

    public decimal? TransferAmt { get; set; }

    public string? Cardno { get; set; }

    public string? Apiresponse { get; set; }

    public string? Cardtype { get; set; }

    public string? ReqBy { get; set; }
}

using System;
using System.Collections.Generic;

namespace InstantPay.Infrastructure.Sql.Entities;

public partial class Tblapionlinepayment
{
    public int Id { get; set; }

    public string Apikey { get; set; } = null!;

    public string? UserId { get; set; }

    public string? UserName { get; set; }

    public string? MobileNo { get; set; }

    public string? CustomerName { get; set; }

    public string? Emailid { get; set; }

    public string? CallbackUrl { get; set; }

    public decimal? Amount { get; set; }

    public decimal? Charge { get; set; }

    public decimal? Gst { get; set; }

    public decimal? SattelAmount { get; set; }

    public string? Status { get; set; }

    public string? TxnId { get; set; }

    public string? ClientRefId { get; set; }

    public string? OrderId { get; set; }

    public string? ApiRequest { get; set; }

    public string? ApiResponse { get; set; }

    public DateTime? ReqDate { get; set; }

    public DateTime? ApprovedDate { get; set; }

    public string? Utrno { get; set; }

    public string? Callbackresponse { get; set; }

    public string? Cstatus { get; set; }
}

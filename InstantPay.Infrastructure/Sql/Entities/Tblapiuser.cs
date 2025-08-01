using System;
using System.Collections.Generic;

namespace InstantPay.Infrastructure.Sql.Entities;

public partial class Tblapiuser
{
    public int Id { get; set; }

    public string ApiKey { get; set; } = null!;

    public string? CompanyName { get; set; }

    public string? FullName { get; set; }

    public string? MobileNo { get; set; }

    public string? EmailId { get; set; }

    public string? Password { get; set; }

    public string? Address { get; set; }

    public string? Pincode { get; set; }

    public string? PanNo { get; set; }

    public string? AadharNo { get; set; }

    public DateTime? Reqdate { get; set; }

    public int? Status { get; set; }

    public string? CallbackUrl { get; set; }

    public decimal? Pay11000 { get; set; }

    public decimal? Pay100124999 { get; set; }

    public decimal? Pay25000 { get; set; }

    public string? PayType { get; set; }

    public string? AadharFront { get; set; }

    public string? AadharBack { get; set; }

    public string? Pancopy { get; set; }

    public string? Profilepic { get; set; }

    public decimal? Gstper { get; set; }

    public string? Payoutstatus { get; set; }

    public string? Payinstatus { get; set; }

    public decimal? Payin1999 { get; set; }

    public decimal? Payin1000 { get; set; }

    public decimal? Paygst { get; set; }

    public string? Ipaddress { get; set; }

    public decimal? PayinLimit { get; set; }

    public decimal? PayoutCapping { get; set; }
}

using System;
using System.Collections.Generic;

namespace InstantPay.Infrastructure.Sql.Entities;

public partial class Tbloperator
{
    public int Id { get; set; }

    public string? ServiceName { get; set; }

    public string? ServiceId { get; set; }

    public string? OperatorName { get; set; }

    public string? Spkey { get; set; }

    public string? Status { get; set; }

    public decimal? Ipshare { get; set; }

    public decimal? Wlshare { get; set; }

    public string? Servicetype { get; set; }

    public string? CommissionType { get; set; }

    public string? Apiname { get; set; }

    public string? Picture { get; set; }
}

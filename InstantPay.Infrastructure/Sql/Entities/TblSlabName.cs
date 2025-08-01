using System;
using System.Collections.Generic;

namespace InstantPay.Infrastructure.Sql.Entities;

public partial class TblSlabName
{
    public int Id { get; set; }

    public string? ServiceName { get; set; }

    public string? ServiceId { get; set; }

    public string? SlabName { get; set; }

    public decimal? Ipshare { get; set; }

    public decimal? Wlshare { get; set; }

    public string? DistributionType { get; set; }

    public string? CommissionType { get; set; }

    public string? Status { get; set; }
}

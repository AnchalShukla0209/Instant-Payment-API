using System;
using System.Collections.Generic;

namespace InstantPay.Infrastructure.Sql.Entities;

public partial class Tblcommissionslab
{
    public int Id { get; set; }

    public string? Wlid { get; set; }

    public string? SlabId { get; set; }

    public string SlabName { get; set; } = null!;

    public string? ServiceName { get; set; }

    public string? ServiceId { get; set; }

    public decimal? Ipshare { get; set; }

    public decimal? WlShare { get; set; }

    public decimal? Mdshare { get; set; }

    public decimal? Adshare { get; set; }

    public decimal? Rtshare { get; set; }

    public string? DistributionType { get; set; }

    public string? CommissionType { get; set; }

    public string? PlanId { get; set; }
}

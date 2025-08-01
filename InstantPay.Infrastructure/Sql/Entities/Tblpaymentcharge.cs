using System;
using System.Collections.Generic;

namespace InstantPay.Infrastructure.Sql.Entities;

public partial class Tblpaymentcharge
{
    public int Id { get; set; }

    public string? SlabName { get; set; }

    public decimal? Charge { get; set; }

    public string? ChargeType { get; set; }
}

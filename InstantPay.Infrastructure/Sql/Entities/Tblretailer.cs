using System;
using System.Collections.Generic;

namespace InstantPay.Infrastructure.Sql.Entities;

public partial class Tblretailer
{
    public int Id { get; set; }

    public string? RetailerName { get; set; }

    public string? MobileNo { get; set; }

    public string? EmailId { get; set; }

    public string? Password { get; set; }

    public string? Status { get; set; }

    public DateTime? RegDate { get; set; }
}

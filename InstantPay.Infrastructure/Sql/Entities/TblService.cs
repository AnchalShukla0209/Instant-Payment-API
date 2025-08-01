using System;
using System.Collections.Generic;

namespace InstantPay.Infrastructure.Sql.Entities;

public partial class TblService
{
    public int Id { get; set; }

    public string? ServiceName { get; set; }

    public string? Status { get; set; }

    public string? Category { get; set; }
}

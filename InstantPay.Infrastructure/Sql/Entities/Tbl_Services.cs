using System;
using System.Collections.Generic;

namespace InstantPay.Infrastructure.Sql.Entities;

public partial class Tbl_Services
{
    public int Id { get; set; }

    public string? ServiceName { get; set; }

    public string? ServicePath { get; set; }

    public bool? IsActive { get; set; }

    public bool? IsDeleted { get; set; }
}

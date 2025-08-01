using System;
using System.Collections.Generic;

namespace InstantPay.Infrastructure.Sql.Entities;

public partial class Tblcommplan
{
    public int Id { get; set; }

    public string? UserType { get; set; }

    public string? PlanName { get; set; }

    public string? Remarks { get; set; }

    public string? UserId { get; set; }

    public DateTime? Reqdate { get; set; }
}

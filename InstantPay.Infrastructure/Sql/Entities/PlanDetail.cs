using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace InstantPay.Infrastructure.Sql.Entities;

public partial class PlanDetail
{
    public int Id { get; set; }

    public string PlanName { get; set; } = null!;

    public bool IsActive { get; set; } = true;

    public string? CreatedBy { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;
}

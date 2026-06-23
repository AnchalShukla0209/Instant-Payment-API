using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace InstantPay.Infrastructure.Sql.Entities;

public partial class CommissionPlan
{
    public int Id { get; set; }

    public int PlanId { get; set; }

    public string SlabRange { get; set; } = null!;

    public decimal AdminShare { get; set; }

    public decimal WlAdminShare { get; set; }

    public decimal MdShare { get; set; }

    public decimal AdShare { get; set; }

    public decimal RtShare { get; set; }

    public string CommissionType { get; set; } = null!;

    public int ServiceId { get; set; }

    public string? APICode { get; set; }

    public int? OperatorId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public string? CreatedBy { get; set; }
}

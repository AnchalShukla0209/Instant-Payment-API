using System;

namespace InstantPay.Application.DTOs;

public class CommissionPlanDto
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
    public DateTime CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public string? PlanName { get; set; }
    public string? ServiceName { get; set; }
    public string? APIName { get; set; }
}

public class CreateCommissionPlanDto
{
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
    public string? CreatedBy { get; set; }
}

public class UpdateCommissionPlanDto
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
}

public class CommissionPlanDropdownDto
{
    public int Id { get; set; }
    public string SlabRange { get; set; } = null!;
    public string PlanName { get; set; } = null!;
}

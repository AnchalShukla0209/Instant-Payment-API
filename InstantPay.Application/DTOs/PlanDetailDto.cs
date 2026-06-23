using System;

namespace InstantPay.Application.DTOs;

public class PlanDetailDto
{
    public int Id { get; set; }
    public string PlanName { get; set; } = null!;
    public bool IsActive { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreatePlanDetailDto
{
    public string PlanName { get; set; } = null!;
    public bool IsActive { get; set; } = true;
    public string? CreatedBy { get; set; }
}

public class UpdatePlanDetailDto
{
    public int Id { get; set; }
    public string PlanName { get; set; } = null!;
    public bool IsActive { get; set; }
}

public class PlanDetailDropdownDto
{
    public int Id { get; set; }
    public string PlanName { get; set; } = null!;
}

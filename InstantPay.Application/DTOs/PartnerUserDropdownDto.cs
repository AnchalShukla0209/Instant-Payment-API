namespace InstantPay.Application.DTOs;

/// <summary>Lightweight downline-user entry for the AD/MD transaction report's "User" filter.</summary>
public class PartnerUserDropdownDto
{
    public int Id { get; set; }
    public string Label { get; set; } = null!;
}

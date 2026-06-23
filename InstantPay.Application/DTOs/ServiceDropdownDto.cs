namespace InstantPay.Application.DTOs;

public class ServiceDropdownDto
{
    public int Id { get; set; }
    public string? ServiceName { get; set; }
    public string? Icon { get; set; }
    public bool? IsActive { get; set; }
    public bool? IsActiveOnApk { get; set; }
}

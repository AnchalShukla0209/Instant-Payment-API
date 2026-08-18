using System.ComponentModel.DataAnnotations;

namespace InstantPay.Application.DTOs;

public sealed class WebsiteEnquiryRequest
{
    [Required, StringLength(100, MinimumLength = 2)]
    public string FullName { get; set; } = string.Empty;

    [Required, RegularExpression(@"^[0-9]{10}$", ErrorMessage = "Mobile number must contain exactly 10 digits.")]
    public string Mobile { get; set; } = string.Empty;

    [Required, EmailAddress, StringLength(180)]
    public string Email { get; set; } = string.Empty;

    [Required, RegularExpression("^(Retailer|Distributor|Master Distributor|Sales Partner)$", ErrorMessage = "Please select a valid opportunity.")]
    public string Interest { get; set; } = string.Empty;

    [StringLength(1200)]
    public string? Message { get; set; }
}

using System.ComponentModel.DataAnnotations;

namespace InstantPay.Application.DTOs;

public class BeneficiaryDto
{
    public int Id { get; set; }
    public bool Status { get; set; }
    public string Name { get; set; } = string.Empty;
    public string AccountNumber { get; set; } = string.Empty;
    public string BankName { get; set; } = string.Empty;
    public string Ifsc { get; set; } = string.Empty;
    public string CustomerNumber { get; set; } = string.Empty;
    public DateTime CreatedOn { get; set; }
    public DateTime UpdatedOn { get; set; }
}

public class SaveBeneficiaryRequest
{
    [Required]
    public string CustomerNumber { get; set; } = string.Empty;
    [Required]
    public string Name { get; set; } = string.Empty;
    [Required]
    public string AccountNumber { get; set; } = string.Empty;
    [Required]
    public string BankName { get; set; } = string.Empty;
    [Required]
    public string Ifsc { get; set; } = string.Empty;
}

public class SaveBeneficiaryResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public BeneficiaryDto? Beneficiary { get; set; }
}

public class SendOtpRequest
{
    [Required]
    public string CustomerNumber { get; set; } = string.Empty;
}

public class SendOtpResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string OtpExpiryTime { get; set; } = string.Empty;
}

public class DeleteBeneficiaryRequest
{
    [Required]
    public string CustomerNumber { get; set; } = string.Empty;
    [Required]
    public int BeneficiaryId { get; set; }
    [Required]
    public string Otp { get; set; } = string.Empty;
}

public class DeleteBeneficiaryResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
}

public class GetBeneficiaryListRequest
{
    [Required]
    public string CustomerNumber { get; set; } = string.Empty;
}

public class GetBeneficiaryListResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public List<BeneficiaryDto> Beneficiaries { get; set; } = new();
}

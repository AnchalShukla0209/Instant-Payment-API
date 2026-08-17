using System.ComponentModel.DataAnnotations;

namespace InstantPay.Application.DTOs;

public static class ClientUserVerificationTypes
{
    public const string Phone = "phone";
    public const string Email = "email";
    public const string Pan = "pan";
    public const string Aadhaar = "aadhaar";
}

public sealed class SendClientUserOtpRequest
{
    [Required]
    public string Value { get; set; } = string.Empty;

    public int ClientId { get; set; }
}

public sealed class VerifyClientUserOtpRequest
{
    [Required]
    public Guid ChallengeId { get; set; }

    [Required]
    [RegularExpression(@"^\d{6}$")]
    public string Otp { get; set; } = string.Empty;

    [Required]
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Optional: when editing an existing client, pass the client id so the
    /// verification status is persisted to the database immediately.
    /// </summary>
    public int ClientId { get; set; }
}

public sealed class VerifyClientUserPanRequest
{
    [Required]
    public string PanNumber { get; set; } = string.Empty;

    public int ClientId { get; set; }
}

public sealed class VerifyClientUserAadhaarRequest
{
    [Required]
    public string AadharNumber { get; set; } = string.Empty;

    public int ClientId { get; set; }
}

public sealed class ClientUserVerificationResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public Guid? ChallengeId { get; set; }
    public string? VerificationToken { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public string? VerifiedName { get; set; }
}

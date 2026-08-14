using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace InstantPay.Application.DTOs;

public sealed record DistributorLoginRequest
{
    [Required, StringLength(80, MinimumLength = 3)]
    public string Username { get; init; } = string.Empty;

    [Required, StringLength(128, MinimumLength = 8)]
    public string Password { get; init; } = string.Empty;

    [Required, RegularExpression("^(web|apk)$", ErrorMessage = "Platform must be web or apk.")]
    public string Platform { get; init; } = string.Empty;

    [Required, StringLength(128, MinimumLength = 8)]
    public string DeviceId { get; init; } = string.Empty;
}

public sealed record DistributorOtpRequest
{
    [Required]
    public Guid ChallengeId { get; init; }

    [Required, RegularExpression("^\\d{6}$", ErrorMessage = "OTP must contain 6 digits.")]
    public string Otp { get; init; } = string.Empty;
}

public sealed record DistributorLoginChallengeResponse(
    bool OtpRequired,
    Guid? ChallengeId,
    string? MaskedMobile,
    int ExpiresInSeconds,
    DistributorTokenResponse? Session);

public sealed record DistributorTokenResponse(
    string AccessToken,
    string TokenType,
    int ExpiresInSeconds,
    string UserId,
    string Username,
    string UserType,
    string DisplayName);

public sealed record DistributorAuthResult<T>(
    bool Succeeded,
    int StatusCode,
    string Code,
    string Message,
    T? Data)
{
    public static DistributorAuthResult<T> Success(T data) =>
        new(true, StatusCodes.Status200OK, "SUCCESS", string.Empty, data);

    public static DistributorAuthResult<T> Failure(int statusCode, string code, string message) =>
        new(false, statusCode, code, message, default);
}

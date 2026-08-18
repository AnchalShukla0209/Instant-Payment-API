using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Globalization;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using InstantPay.Application.DTOs;
using InstantPay.Application.Interfaces;
using InstantPay.Infrastructure.Sql.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

namespace InstantPay.Application.Services;

public sealed class DistributorAuthService : IDistributorAuthService
{
    private const string DistributorUserType = "AD";
    private const string MasterDistributorUserType = "MD";
    private const string SalesTeamUserType = "ST";
    private const int MaxPasswordAttempts = 5;
    private const int MaxOtpAttempts = 5;
    private const int PasswordHashWorkFactor = 12;
    private static readonly TimeSpan AccountLockoutDuration = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan OtpLifetime = TimeSpan.FromMinutes(5);
    private static readonly string DummyPasswordHash =
        BCrypt.Net.BCrypt.HashPassword("invalid-distributor-password", PasswordHashWorkFactor);

    private readonly AppDbContext _dbContext;
    private readonly IOtpService _otpService;
    private readonly IConfiguration _configuration;
    private readonly IHostEnvironment _hostEnvironment;
    private readonly ILogger<DistributorAuthService> _logger;

    public DistributorAuthService(
        AppDbContext dbContext,
        IOtpService otpService,
        IConfiguration configuration,
        IHostEnvironment hostEnvironment,
        ILogger<DistributorAuthService> logger)
    {
        _dbContext = dbContext;
        _otpService = otpService;
        _configuration = configuration;
        _hostEnvironment = hostEnvironment;
        _logger = logger;
    }

    public async Task<DistributorAuthResult<DistributorLoginChallengeResponse>> LoginAsync(
        DistributorLoginRequest request,
        string expectedUserType,
        string ipAddress,
        CancellationToken cancellationToken)
    {
        var userType = NormalizePartnerUserType(expectedUserType);
        var username = request.Username.Trim();
        var user = await _dbContext.TblUsers
            .SingleOrDefaultAsync(
                candidate => candidate.Username == username &&
                             candidate.Usertype == userType &&
                             candidate.Status == "Active",
                cancellationToken);

        if (user is null)
        {
            BCrypt.Net.BCrypt.Verify(request.Password, DummyPasswordHash);
            return InvalidCredentials();
        }

        if (user.LockoutEnd is { } lockoutEnd && lockoutEnd > DateTime.UtcNow)
        {
            return DistributorAuthResult<DistributorLoginChallengeResponse>.Failure(
                StatusCodes.Status423Locked,
                "ACCOUNT_LOCKED",
                "Unable to sign in. Please try again later or contact support.");
        }

        if (!VerifyPassword(request.Password, user.Password))
        {
            await RegisterFailedPasswordAttemptAsync(user, userType, ipAddress, cancellationToken);
            var remainingAttempts = Math.Max(0, MaxPasswordAttempts - (user.FailedUnlockAttempts ?? 0));
            if (remainingAttempts == 0)
            {
                return DistributorAuthResult<DistributorLoginChallengeResponse>.Failure(
                    StatusCodes.Status423Locked,
                    "ACCOUNT_LOCKED",
                    "Account has been locked for 15 minutes after repeated invalid passwords.");
            }

            if (remainingAttempts <= 2)
            {
                return DistributorAuthResult<DistributorLoginChallengeResponse>.Failure(
                    StatusCodes.Status401Unauthorized,
                    "INVALID_CREDENTIALS_WARNING",
                    $"Invalid password. {remainingAttempts} attempt(s) remaining before the account is locked.");
            }

            return InvalidCredentials();
        }

        user.FailedUnlockAttempts = 0;
        user.LockoutEnd = null;
        await _dbContext.SaveChangesAsync(cancellationToken);

        var platform = request.Platform.ToLowerInvariant();
        var deviceFingerprint = ComputeDeviceFingerprint(platform, request.DeviceId);
        var trustedSinceUtc = GetIndiaDayStartUtc();
        var isTrustedForToday = await _dbContext.Tblloginlogs.AnyAsync(
            log => log.UserId == user.Id.ToString() &&
                   log.Usertype == userType &&
                   log.OTPVerified &&
                   log.DeviceType == platform &&
                   log.BrowserFingerprint == deviceFingerprint &&
                   log.LoginTime >= trustedSinceUtc,
            cancellationToken);

        if (isTrustedForToday)
        {
            SetLoginStatus(user, platform);
            AddLoginAudit(user.Id, userType, platform, deviceFingerprint, ipAddress, otpVerified: false);
            await _dbContext.SaveChangesAsync(cancellationToken);

            return DistributorAuthResult<DistributorLoginChallengeResponse>.Success(
                new DistributorLoginChallengeResponse(
                    false,
                    null,
                    null,
                    0,
                    CreateTokenResponse(user, userType, ipAddress)));
        }

        if (string.IsNullOrWhiteSpace(user.Phone))
        {
            _logger.LogWarning("Distributor {UserId} has no registered mobile number.", user.Id);
            return DistributorAuthResult<DistributorLoginChallengeResponse>.Failure(
                StatusCodes.Status409Conflict,
                "MOBILE_NOT_REGISTERED",
                "A registered mobile number is required. Please contact support.");
        }

        var challengeId = Guid.NewGuid();
        var developmentOtpSection = userType == MasterDistributorUserType
            ? "MasterDistributorAuth"
            : "DistributorAuth";
        var configuredDevelopmentOtp = _hostEnvironment.IsDevelopment()
            ? _configuration[$"{developmentOtpSection}:DevelopmentOtp"]
            : null;
        var useDevelopmentOtp = configuredDevelopmentOtp is { Length: 6 } &&
                                configuredDevelopmentOtp.All(char.IsDigit);
        var otp = useDevelopmentOtp
            ? configuredDevelopmentOtp!
            : RandomNumberGenerator.GetInt32(100000, 1_000_000).ToString();
        var challengeKey = BuildChallengeKey(challengeId, user.Id, userType, platform, deviceFingerprint);
        var storedOtp = new TblloginOtp
        {
            UserId = challengeKey,
            OTP = ComputeOtpHash(challengeId, otp),
            IsUsed = false,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.Add(OtpLifetime)
        };

        try
        {
            _dbContext.TblloginOtps.Add(storedOtp);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Could not persist distributor OTP challenge for user {UserId}.", user.Id);
            return DistributorAuthResult<DistributorLoginChallengeResponse>.Failure(
                StatusCodes.Status503ServiceUnavailable,
                "OTP_SESSION_FAILED",
                "We could not start the verification session. Please try again.");
        }

        if (useDevelopmentOtp)
        {
            _logger.LogWarning(
                "Distributor OTP delivery skipped because the Development-only test OTP is enabled.");
        }
        else
        {
            try
            {
                await _otpService.SendOtpAsync(user.Phone.Trim(), otp);
            }
            catch (Exception exception)
            {
                _dbContext.TblloginOtps.Remove(storedOtp);
                await _dbContext.SaveChangesAsync(cancellationToken);
                _logger.LogError(exception, "Could not deliver distributor login OTP for user {UserId}.", user.Id);
                return DistributorAuthResult<DistributorLoginChallengeResponse>.Failure(
                    StatusCodes.Status503ServiceUnavailable,
                    "OTP_DELIVERY_FAILED",
                    "We could not send the verification code. Please try again.");
            }
        }

        _logger.LogInformation(
            "Distributor login challenge created for user {UserId} from {IpAddress}.",
            user.Id,
            SafeIpAddress(ipAddress));

        return DistributorAuthResult<DistributorLoginChallengeResponse>.Success(
            new DistributorLoginChallengeResponse(
                true,
                challengeId,
                MaskMobile(user.Phone),
                (int)OtpLifetime.TotalSeconds,
                null));
    }

    public async Task<DistributorAuthResult<DistributorTokenResponse>> VerifyOtpAsync(
        DistributorOtpRequest request,
        string expectedUserType,
        string ipAddress,
        CancellationToken cancellationToken)
    {
        var userType = NormalizePartnerUserType(expectedUserType);
        var prefix = $"{EncodeChallengeId(request.ChallengeId)}|";
        var storedOtp = await _dbContext.TblloginOtps
            .Where(candidate => candidate.UserId != null &&
                                candidate.UserId.StartsWith(prefix) &&
                                candidate.IsUsed == false)
            .OrderByDescending(candidate => candidate.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (storedOtp?.ExpiresAt is null || storedOtp.ExpiresAt < DateTime.UtcNow)
        {
            if (storedOtp is not null)
            {
                _dbContext.TblloginOtps.Remove(storedOtp);
                await _dbContext.SaveChangesAsync(cancellationToken);
            }
            return InvalidOtp("OTP_EXPIRED", "The verification session has expired. Please sign in again.");
        }

        var challengeParts = storedOtp.UserId!.Split('|');
        if (challengeParts.Length != 4 ||
            !int.TryParse(
                challengeParts[1],
                NumberStyles.HexNumber,
                CultureInfo.InvariantCulture,
                out var userId) ||
            !TryDecodeChallengeContext(challengeParts[2], out var challengeUserType, out var platform) ||
            challengeUserType != userType)
        {
            _dbContext.TblloginOtps.Remove(storedOtp);
            await _dbContext.SaveChangesAsync(cancellationToken);
            return InvalidOtp("INVALID_SESSION", "The verification session is no longer valid.");
        }

        var deviceFingerprint = challengeParts[3];
        var expectedOtpHash = ComputeOtpHash(request.ChallengeId, request.Otp);
        if (string.IsNullOrWhiteSpace(storedOtp.OTP) ||
            !CryptographicOperations.FixedTimeEquals(
                Encoding.ASCII.GetBytes(storedOtp.OTP),
                Encoding.ASCII.GetBytes(expectedOtpHash)))
        {
            var eventPrefix = $"FAILED_{userType}_OTP|{request.ChallengeId:N}|";
            var failedAttempts = await _dbContext.TblPasswordattmts.CountAsync(
                attempt => attempt.UserId == userId.ToString() &&
                           attempt.Password != null &&
                           attempt.Password.StartsWith(eventPrefix),
                cancellationToken) + 1;

            _dbContext.TblPasswordattmts.Add(new TblPasswordattmt
            {
                UserId = userId.ToString(),
                Ipaddress = SafeIpAddress(ipAddress),
                Password = $"{eventPrefix}ATTEMPT:{failedAttempts}",
                Reqdate = DateTime.UtcNow
            });

            if (failedAttempts >= MaxOtpAttempts)
            {
                _dbContext.TblloginOtps.Remove(storedOtp);
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
            return InvalidOtp("INVALID_OTP", "The verification code is invalid or expired.");
        }

        var user = await _dbContext.TblUsers.SingleOrDefaultAsync(
            candidate => candidate.Id == userId &&
                         candidate.Usertype == userType &&
                         candidate.Status == "Active",
            cancellationToken);

        if (user is null)
        {
            _dbContext.TblloginOtps.Remove(storedOtp);
            await _dbContext.SaveChangesAsync(cancellationToken);
            return InvalidOtp("INVALID_SESSION", "The verification session is no longer valid.");
        }

        _dbContext.TblloginOtps.Remove(storedOtp);
        SetLoginStatus(user, platform);
        AddLoginAudit(user.Id, userType, platform, deviceFingerprint, ipAddress, otpVerified: true);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Distributor {UserId} completed MFA login from {IpAddress}.",
            user.Id,
            SafeIpAddress(ipAddress));

        return DistributorAuthResult<DistributorTokenResponse>.Success(CreateTokenResponse(user, userType, ipAddress));
    }

    private async Task RegisterFailedPasswordAttemptAsync(
        TblUser user,
        string userType,
        string ipAddress,
        CancellationToken cancellationToken)
    {
        user.FailedUnlockAttempts = (user.FailedUnlockAttempts ?? 0) + 1;
        if (user.FailedUnlockAttempts >= MaxPasswordAttempts)
        {
            user.LockoutEnd = DateTime.UtcNow.Add(AccountLockoutDuration);
        }

        _dbContext.TblPasswordattmts.Add(new TblPasswordattmt
        {
            UserId = user.Id.ToString(),
            Ipaddress = SafeIpAddress(ipAddress),
            Password = $"FAILED_{userType}_LOGIN|ATTEMPT:{user.FailedUnlockAttempts}",
            Reqdate = DateTime.UtcNow
        });

        await _dbContext.SaveChangesAsync(cancellationToken);
        _logger.LogWarning(
            "Failed distributor password attempt {Attempt} for user {UserId} from {IpAddress}.",
            user.FailedUnlockAttempts,
            user.Id,
            SafeIpAddress(ipAddress));
    }

    private string GenerateAccessToken(TblUser user, string userType)
    {
        var keyValue = _configuration["Jwt:Key"]
            ?? throw new InvalidOperationException("Jwt:Key is not configured.");
        if (Encoding.UTF8.GetByteCount(keyValue) < 32)
        {
            throw new InvalidOperationException("Jwt:Key must be at least 256 bits.");
        }

        var now = DateTime.UtcNow;
        var accessTokenLifetime = GetAccessTokenLifetime();
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.UniqueName, user.Username ?? string.Empty),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
            new Claim("userid", user.Id.ToString()),
            new Claim("username", user.Username ?? string.Empty),
            new Claim("usertype", userType),
            new Claim("auth_time", new DateTimeOffset(now).ToUnixTimeSeconds().ToString()),
            new Claim("amr", "pwd"),
            new Claim("amr", "otp"),
            new Claim(
                ClaimTypes.Role,
                GetRoleName(userType))
        };

        var token = new JwtSecurityToken(
            issuer: _configuration["Jwt:Issuer"],
            audience: _configuration["Jwt:Audience"],
            claims: claims,
            notBefore: now,
            expires: now.Add(accessTokenLifetime),
            signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(keyValue)),
                SecurityAlgorithms.HmacSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private DistributorTokenResponse CreateTokenResponse(TblUser user, string userType, string ipAddress) =>
        new(
            GenerateAccessToken(user, userType),
            "Bearer",
            (int)GetAccessTokenLifetime().TotalSeconds,
            user.Id.ToString(),
            user.Username ?? string.Empty,
            userType,
            user.Name ?? user.CompanyName ?? GetDisplayRole(userType),
            DateTime.UtcNow,
            SafeIpAddress(ipAddress));

    private TimeSpan GetAccessTokenLifetime()
    {
        var configuredMinutes = _configuration.GetValue<int?>("Jwt:ExpiresInMinutes") ?? 60;
        return TimeSpan.FromMinutes(Math.Clamp(configuredMinutes, 15, 60));
    }

    private void SetLoginStatus(TblUser user, string platform)
    {
        if (platform == "web")
        {
            user.IsUserLoggedInFromWeb = true;
        }
        else
        {
            user.IsUserLoggedInFromApk = true;
        }
    }

    private void AddLoginAudit(
        int userId,
        string userType,
        string platform,
        string deviceFingerprint,
        string ipAddress,
        bool otpVerified)
    {
        _dbContext.Tblloginlogs.Add(new Tblloginlog
        {
            UserId = userId.ToString(),
            Usertype = userType,
            Ipaddress = SafeIpAddress(ipAddress),
            LoginTime = DateTime.UtcNow,
            OTPVerified = otpVerified,
            DeviceType = platform,
            BrowserFingerprint = deviceFingerprint
        });
    }

    private string ComputeDeviceFingerprint(string platform, string deviceId)
    {
        var secret = _configuration["Jwt:Key"]
            ?? throw new InvalidOperationException("Jwt:Key is not configured.");
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var digest = Convert.ToHexString(
            hmac.ComputeHash(Encoding.UTF8.GetBytes($"{platform}:{deviceId.Trim()}")));
        return digest[..16];
    }

    private static DateTime GetIndiaDayStartUtc()
    {
        var indiaOffset = TimeSpan.FromMinutes(330);
        var indiaNow = DateTimeOffset.UtcNow.ToOffset(indiaOffset);
        return new DateTimeOffset(indiaNow.Date, indiaOffset).UtcDateTime;
    }

    private static string BuildChallengeKey(
        Guid challengeId,
        int userId,
        string userType,
        string platform,
        string deviceFingerprint)
    {
        var contextCode = (userType, platform) switch
        {
            (DistributorUserType, "web") => "w",
            (DistributorUserType, "apk") => "a",
            (MasterDistributorUserType, "web") => "m",
            (MasterDistributorUserType, "apk") => "n",
            (SalesTeamUserType, "web") => "s",
            (SalesTeamUserType, "apk") => "t",
            _ => throw new ArgumentOutOfRangeException(nameof(platform))
        };
        return $"{EncodeChallengeId(challengeId)}|{userId:X}|{contextCode}|{deviceFingerprint}";
    }

    private static bool TryDecodeChallengeContext(
        string contextCode,
        out string userType,
        out string platform)
    {
        (userType, platform) = contextCode switch
        {
            "w" => (DistributorUserType, "web"),
            "a" => (DistributorUserType, "apk"),
            "m" => (MasterDistributorUserType, "web"),
            "n" => (MasterDistributorUserType, "apk"),
            "s" => (SalesTeamUserType, "web"),
            "t" => (SalesTeamUserType, "apk"),
            _ => (string.Empty, string.Empty)
        };
        return userType.Length > 0;
    }

    private static string NormalizePartnerUserType(string userType) =>
        userType.ToUpperInvariant() switch
        {
            DistributorUserType => DistributorUserType,
            MasterDistributorUserType => MasterDistributorUserType,
            SalesTeamUserType => SalesTeamUserType,
            _ => throw new ArgumentOutOfRangeException(nameof(userType))
        };

    private static string GetRoleName(string userType) => userType switch
    {
        DistributorUserType => "Distributor",
        MasterDistributorUserType => "MasterDistributor",
        SalesTeamUserType => "SalesTeam",
        _ => throw new ArgumentOutOfRangeException(nameof(userType))
    };

    private static string GetDisplayRole(string userType) => userType switch
    {
        DistributorUserType => "Distributor",
        MasterDistributorUserType => "Master Distributor",
        SalesTeamUserType => "Sales Team",
        _ => "Partner"
    };

    private static string EncodeChallengeId(Guid challengeId) =>
        Convert.ToBase64String(challengeId.ToByteArray())
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

    private string ComputeOtpHash(Guid challengeId, string otp)
    {
        var secret = _configuration["Jwt:Key"]
            ?? throw new InvalidOperationException("Jwt:Key is not configured.");
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var digest = Convert.ToHexString(
            hmac.ComputeHash(Encoding.UTF8.GetBytes($"{challengeId:N}:{otp}")));

        // tblLoginOTP.OTP is varchar(10). A keyed 40-bit digest fits the
        // existing schema and avoids storing the six-digit OTP in plaintext.
        return digest[..10];
    }

    private static bool VerifyPassword(string suppliedPassword, string? storedPassword)
    {
        if (string.IsNullOrEmpty(storedPassword))
        {
            return false;
        }

        if (IsBcryptHash(storedPassword))
        {
            try
            {
                return BCrypt.Net.BCrypt.Verify(suppliedPassword, storedPassword);
            }
            catch (BCrypt.Net.SaltParseException)
            {
                return false;
            }
        }

        var suppliedHash = SHA256.HashData(Encoding.UTF8.GetBytes(suppliedPassword));
        var storedHash = SHA256.HashData(Encoding.UTF8.GetBytes(storedPassword));
        return CryptographicOperations.FixedTimeEquals(suppliedHash, storedHash);
    }

    private static bool IsBcryptHash(string? value) =>
        value?.StartsWith("$2", StringComparison.Ordinal) == true;

    private static string MaskMobile(string mobile)
    {
        var digits = new string(mobile.Where(char.IsDigit).ToArray());
        return digits.Length < 4 ? "******" : $"******{digits[^4..]}";
    }

    private static string SafeIpAddress(string ipAddress)
    {
        var value = Truncate(ipAddress, 64);
        if (value is null)
        {
            return "unknown";
        }

        return IPAddress.TryParse(value, out var parsed) && parsed.IsIPv4MappedToIPv6
            ? parsed.MapToIPv4().ToString()
            : value;
    }

    private static string? Truncate(string? value, int maximumLength) =>
        string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim()[..Math.Min(value.Trim().Length, maximumLength)];

    private static DistributorAuthResult<DistributorLoginChallengeResponse> InvalidCredentials() =>
        DistributorAuthResult<DistributorLoginChallengeResponse>.Failure(
            StatusCodes.Status401Unauthorized,
            "INVALID_CREDENTIALS",
            "The username or password is incorrect.");

    private static DistributorAuthResult<DistributorTokenResponse> InvalidOtp(string code, string message) =>
        DistributorAuthResult<DistributorTokenResponse>.Failure(
            StatusCodes.Status401Unauthorized,
            code,
            message);

}

using System.Net.Mail;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using InstantPay.Application.DTOs;
using InstantPay.Application.Interfaces;
using InstantPay.Application.Interfaces.Aadhaar;
using InstantPay.Application.Interfaces.PAN;
using InstantPay.Infrastructure.Sql.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace InstantPay.Application.Services;

public sealed class ClientUserVerificationService : IClientUserVerificationService
{
    private static readonly TimeSpan OtpLifetime = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan ProofLifetime = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan ResendCooldown = TimeSpan.FromSeconds(30);
    private const int MaximumAttempts = 5;

    private readonly IMemoryCache _cache;
    private readonly IOtpService _otpService;
    private readonly IEmailService _emailService;
    private readonly IPanService _panService;
    private readonly IAadhaarService _aadhaarService;
    private readonly AppDbContext _context;

    public ClientUserVerificationService(
        IMemoryCache cache,
        IOtpService otpService,
        IEmailService emailService,
        IPanService panService,
        IAadhaarService aadhaarService,
        AppDbContext context)
    {
        _cache = cache;
        _otpService = otpService;
        _emailService = emailService;
        _panService = panService;
        _aadhaarService = aadhaarService;
        _context = context;
    }

    public Task<ClientUserVerificationResponse> SendPhoneOtpAsync(string phone)
    {
        var normalized = Normalize(ClientUserVerificationTypes.Phone, phone);
        if (!Regex.IsMatch(normalized, @"^[6-9]\d{9}$"))
            return Task.FromResult(Fail("Enter a valid 10-digit mobile number."));

        return SendOtpAsync(ClientUserVerificationTypes.Phone, normalized);
    }

    public Task<ClientUserVerificationResponse> SendEmailOtpAsync(string email)
    {
        var normalized = Normalize(ClientUserVerificationTypes.Email, email);
        try
        {
            _ = new MailAddress(normalized);
        }
        catch
        {
            return Task.FromResult(Fail("Enter a valid email address."));
        }

        return SendOtpAsync(ClientUserVerificationTypes.Email, normalized);
    }

    public async Task<ClientUserVerificationResponse> VerifyOtpAsync(VerifyClientUserOtpRequest request)
    {
        var type = request.Type.Trim().ToLowerInvariant();
        if (type is not (ClientUserVerificationTypes.Phone or ClientUserVerificationTypes.Email))
            return Fail("Invalid verification type.");

        var key = ChallengeKey(request.ChallengeId);
        if (!_cache.TryGetValue(key, out OtpChallenge? challenge) || challenge == null || challenge.Type != type)
            return Fail("OTP challenge has expired. Please request a new OTP.");

        challenge.Attempts++;
        if (challenge.Attempts > MaximumAttempts)
        {
            _cache.Remove(key);
            return Fail("Too many invalid attempts. Please request a new OTP.");
        }

        if (!BCrypt.Net.BCrypt.Verify(request.Otp, challenge.OtpHash))
            return Fail("Invalid OTP.");

        _cache.Remove(key);
        var token = CreateProof(type, challenge.Value, null);
        await PersistVerificationAsync(request.ClientId, type, challenge.Value, null);
        return new ClientUserVerificationResponse
        {
            Success = true,
            Message = "Verified successfully.",
            VerificationToken = token,
            ExpiresAt = DateTime.UtcNow.Add(ProofLifetime)
        };
    }

    public async Task<ClientUserVerificationResponse> VerifyPanAsync(string panNumber, int clientId = 0)
    {
        var normalized = Normalize(ClientUserVerificationTypes.Pan, panNumber);
        if (!Regex.IsMatch(normalized, @"^[A-Z]{5}[0-9]{4}[A-Z]$"))
            return Fail("Invalid PAN format.");

        var result = await _panService.VerifyPanAsync(normalized);
        if (!result.Success)
            return Fail(result.Message ?? "PAN verification failed.");

        var token = CreateProof(ClientUserVerificationTypes.Pan, normalized, result.Name);
        await PersistVerificationAsync(clientId, ClientUserVerificationTypes.Pan, normalized, result.Name);
        return new ClientUserVerificationResponse
        {
            Success = true,
            Message = "PAN verified successfully.",
            VerificationToken = token,
            ExpiresAt = DateTime.UtcNow.Add(ProofLifetime),
            VerifiedName = result.Name
        };
    }

    public async Task<ClientUserVerificationResponse> VerifyAadhaarAsync(string aadhaarNumber, int clientId = 0)
    {
        var normalized = Normalize(ClientUserVerificationTypes.Aadhaar, aadhaarNumber);
        if (!Regex.IsMatch(normalized, @"^\d{12}$"))
            return Fail("Invalid Aadhaar format.");

        var result = await _aadhaarService.VerifyAadhaarAsync(normalized);
        if (!result.Success)
            return Fail(result.Message ?? "Aadhaar verification failed.");

        var verifiedInfo = string.Join(", ", new[]
        {
            string.IsNullOrWhiteSpace(result.State) ? null : $"State: {result.State}",
            string.IsNullOrWhiteSpace(result.Gender) ? null : $"Gender: {result.Gender}"
        }.Where(v => v != null));

        var token = CreateProof(ClientUserVerificationTypes.Aadhaar, normalized, verifiedInfo);
        await PersistVerificationAsync(clientId, ClientUserVerificationTypes.Aadhaar, normalized, null);
        return new ClientUserVerificationResponse
        {
            Success = true,
            Message = "Aadhaar verified successfully.",
            VerificationToken = token,
            ExpiresAt = DateTime.UtcNow.Add(ProofLifetime),
            VerifiedName = verifiedInfo
        };
    }

    public bool ValidateProof(string? token, string type, string value)
    {
        if (string.IsNullOrWhiteSpace(token))
            return false;

        var normalizedType = type.Trim().ToLowerInvariant();
        var normalizedValue = Normalize(normalizedType, value);
        return _cache.TryGetValue(ProofKey(token), out VerificationProof? proof)
            && proof != null
            && proof.Type == normalizedType
            && proof.Value == normalizedValue;
    }

    public void ConsumeProof(string? token)
    {
        if (!string.IsNullOrWhiteSpace(token))
            _cache.Remove(ProofKey(token));
    }

    public string? GetVerifiedName(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return null;

        return _cache.TryGetValue(ProofKey(token), out VerificationProof? proof)
            ? proof?.VerifiedName
            : null;
    }

    /// <summary>
    /// Immediately persists the verification status (and the verified value) to the
    /// database for an existing client, so re-opening the record never asks for
    /// re-verification of a value that has already been confirmed.
    /// New clients (clientId == 0) are skipped here; their verification is persisted
    /// when the client record is created via CreateOrUpdateClientUser.
    /// </summary>
    private async Task PersistVerificationAsync(int clientId, string type, string value, string? verifiedName)
    {
        if (clientId <= 0)
            return;

        var user = await _context.TblUsers.FirstOrDefaultAsync(u => u.Id == clientId);
        if (user == null)
            return;

        var now = DateTime.UtcNow;
        switch (type)
        {
            case ClientUserVerificationTypes.Phone:
                user.Phone = value;
                user.IsPhoneVerified = true;
                user.PhoneVerifiedAt = now;
                break;
            case ClientUserVerificationTypes.Email:
                user.EmailId = value;
                user.IsEmailVerified = true;
                user.EmailVerifiedAt = now;
                break;
            case ClientUserVerificationTypes.Pan:
                user.PanCard = value;
                user.IsPanVerified = true;
                user.PanVerifiedAt = now;
                user.PanVerifiedName = verifiedName;
                break;
            case ClientUserVerificationTypes.Aadhaar:
                user.AadharCard = value;
                user.IsAadhaarVerified = true;
                user.AadharVerifiedAt = now;
                break;
            default:
                return;
        }

        await _context.SaveChangesAsync();
    }

    private async Task<ClientUserVerificationResponse> SendOtpAsync(string type, string value)
    {
        var cooldownKey = $"client-user-verification:cooldown:{type}:{value}";
        if (_cache.TryGetValue(cooldownKey, out _))
            return Fail("Please wait 30 seconds before requesting another OTP.");

        var otp = RandomNumberGenerator.GetInt32(100000, 1000000).ToString();
        if (type == ClientUserVerificationTypes.Phone)
        {
            await _otpService.SendOtpAsync(value, otp);
        }
        else
        {
            var sendResult = await _emailService.SendClientUserVerificationOtpAsync(value, otp);
            if (sendResult != "1")
                return Fail("Unable to send email OTP.");
        }

        var challengeId = Guid.NewGuid();
        var expiresAt = DateTime.UtcNow.Add(OtpLifetime);
        _cache.Set(
            ChallengeKey(challengeId),
            new OtpChallenge(type, value, BCrypt.Net.BCrypt.HashPassword(otp), 0),
            OtpLifetime);
        _cache.Set(cooldownKey, true, ResendCooldown);

        return new ClientUserVerificationResponse
        {
            Success = true,
            Message = "OTP sent successfully.",
            ChallengeId = challengeId,
            ExpiresAt = expiresAt
        };
    }

    private string CreateProof(string type, string value, string? verifiedName)
    {
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        _cache.Set(
            ProofKey(token),
            new VerificationProof(type, Normalize(type, value), verifiedName),
            ProofLifetime);
        return token;
    }

    private static string Normalize(string type, string value)
    {
        var trimmed = value?.Trim() ?? string.Empty;
        return type switch
        {
            ClientUserVerificationTypes.Email => trimmed.ToLowerInvariant(),
            ClientUserVerificationTypes.Pan => trimmed.ToUpperInvariant(),
            _ => trimmed
        };
    }

    private static string ChallengeKey(Guid id) => $"client-user-verification:challenge:{id:N}";
    private static string ProofKey(string token) => $"client-user-verification:proof:{token}";
    private static ClientUserVerificationResponse Fail(string message) => new() { Success = false, Message = message };

    private sealed record OtpChallenge(string Type, string Value, string OtpHash, int InitialAttempts)
    {
        public int Attempts { get; set; } = InitialAttempts;
    }

    private sealed record VerificationProof(string Type, string Value, string? VerifiedName);
}

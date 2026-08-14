using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using InstantPay.Application.DTOs;
using InstantPay.Application.Interfaces;
using InstantPay.Infrastructure.Security;
using InstantPay.Infrastructure.Sql.Entities;
using InstantPay.SharedKernel.Entity;
using InstantPay.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;

namespace InstantPay.Application.Services;

public sealed class PartnerAccountService : IPartnerAccountService
{
    private const int PasswordHashWorkFactor = 12;

    private readonly AppDbContext _context;
    private readonly IUserRepository _userRepository;
    private readonly IOtpService _otpService;
    private readonly AesEncryptionService _aes;

    public PartnerAccountService(
        AppDbContext context,
        IUserRepository userRepository,
        IOtpService otpService,
        AesEncryptionService aes)
    {
        _context = context;
        _userRepository = userRepository;
        _otpService = otpService;
        _aes = aes;
    }

    public async Task<PartnerAccountProfileDto?> GetProfileAsync(
        int partnerId,
        string userType,
        CancellationToken cancellationToken)
    {
        var user = await GetActivePartnerAsync(partnerId, userType, cancellationToken);
        if (user == null)
        {
            return null;
        }

        return new PartnerAccountProfileDto(
            user.Name ?? user.Username ?? string.Empty,
            user.Username ?? string.Empty,
            user.Phone ?? string.Empty,
            user.PanCard ?? string.Empty,
            user.AadharCard ?? string.Empty);
    }

    public async Task<ResponseSuccess> ValidateAndSendOtpAsync(
        int partnerId,
        string userType,
        PartnerValidateAccountRequest request,
        CancellationToken cancellationToken)
    {
        var user = await GetActivePartnerAsync(partnerId, userType, cancellationToken);
        if (user == null)
        {
            return BuildResponse(false, "Partner account not found.");
        }

        if (!string.Equals(user.PanCard?.Trim(), request.PanNo?.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return BuildResponse(false, "Invalid PAN Card, Please Enter Registered PAN Card");
        }

        if (!string.Equals(user.AadharCard?.Trim(), request.AadharNo?.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return BuildResponse(false, "Invalid Aadhar Number, Please Enter Registered Aadhar Number");
        }

        var otp = Random.Shared.Next(1000, 9999).ToString();
        var encryptedOtp = _aes.Encrypt(otp);

        _context.TblloginOtps.Add(new TblloginOtp
        {
            UserId = partnerId.ToString(),
            IsUsed = false,
            OTP = otp,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddMinutes(10)
        });
        await _context.SaveChangesAsync(cancellationToken);

        await _otpService.SendOtpAsync(user.Phone?.Trim(), otp);

        return BuildResponse(true, encryptedOtp);
    }

    public async Task<ResponseSuccess> ResendOtpAsync(int partnerId, CancellationToken cancellationToken)
    {
        var user = await _context.TblUsers
            .FirstOrDefaultAsync(
                x => x.Id == partnerId &&
                     (x.Usertype == "AD" || x.Usertype == "MD") &&
                     x.Status == "Active",
                cancellationToken);

        if (user == null || string.IsNullOrWhiteSpace(user.Phone))
        {
            return BuildResponse(false, "Unable to resend OTP.");
        }

        var otp = Random.Shared.Next(1000, 9999).ToString();

        _context.TblloginOtps.Add(new TblloginOtp
        {
            UserId = partnerId.ToString(),
            IsUsed = false,
            OTP = otp,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddMinutes(10)
        });
        await _context.SaveChangesAsync(cancellationToken);

        await _otpService.SendOtpAsync(user.Phone.Trim(), otp);

        return BuildResponse(true, "OTP Sent Successfully!");
    }

    public async Task<ResponseSuccess> ChangePasswordAsync(
        int partnerId,
        PartnerChangePasswordRequest request,
        CancellationToken cancellationToken)
    {
        if (!await VerifyOtpAsync(partnerId, request.Otp))
        {
            return BuildResponse(false, "Invalid OTP");
        }

        var user = await _context.TblUsers
            .FirstOrDefaultAsync(
                x => x.Id == partnerId &&
                     (x.Usertype == "AD" || x.Usertype == "MD") &&
                     x.Status == "Active",
                cancellationToken);

        if (user == null)
        {
            return BuildResponse(false, "Partner account not found.");
        }

        if (!VerifyPassword(request.OldPassword ?? string.Empty, user.Password))
        {
            return BuildResponse(false, "Old Password is not correct, Kindly Enter correct old Password !");
        }

        if (!string.Equals(request.NewPassword, request.ConfirmPassword, StringComparison.Ordinal))
        {
            return BuildResponse(false, "New Password And Confirm Password Should match !");
        }

        if (!IsPasswordValid(request.NewPassword))
        {
            return BuildResponse(
                false,
                "Password must contain at least 1 uppercase, 1 lowercase, 1 numeric and 1 special character.");
        }

        user.Password = BCrypt.Net.BCrypt.HashPassword(request.ConfirmPassword!, PasswordHashWorkFactor);
        await _context.SaveChangesAsync(cancellationToken);

        return BuildResponse(true, "Password Changed Successfully !");
    }

    public async Task<ResponseSuccess> ChangeMpinAsync(
        int partnerId,
        PartnerChangeMpinRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Mpin))
        {
            return BuildResponse(false, "MPIN is required.");
        }

        if (!await VerifyOtpAsync(partnerId, request.Otp))
        {
            return BuildResponse(false, "Invalid OTP");
        }

        var user = await _context.TblUsers
            .FirstOrDefaultAsync(
                x => x.Id == partnerId &&
                     (x.Usertype == "AD" || x.Usertype == "MD") &&
                     x.Status == "Active",
                cancellationToken);

        if (user == null)
        {
            return BuildResponse(false, "Partner account not found.");
        }

        user.MPin = request.Mpin.Trim();
        await _context.SaveChangesAsync(cancellationToken);

        return BuildResponse(true, "MPIN Updated Successfully !");
    }

    public async Task<ResponseSuccess> ChangeTxnPinAsync(
        int partnerId,
        PartnerChangeTxnPinRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.TxnPin))
        {
            return BuildResponse(false, "Txn PIN is required.");
        }

        if (!await VerifyOtpAsync(partnerId, request.Otp))
        {
            return BuildResponse(false, "Invalid OTP");
        }

        var user = await _context.TblUsers
            .FirstOrDefaultAsync(
                x => x.Id == partnerId &&
                     (x.Usertype == "AD" || x.Usertype == "MD") &&
                     x.Status == "Active",
                cancellationToken);

        if (user == null)
        {
            return BuildResponse(false, "Partner account not found.");
        }

        user.TxnPin = request.TxnPin.Trim();
        await _context.SaveChangesAsync(cancellationToken);

        return BuildResponse(true, "Txn PIN Updated Successfully !");
    }

    private async Task<TblUser?> GetActivePartnerAsync(
        int partnerId,
        string userType,
        CancellationToken cancellationToken)
    {
        var normalizedType = userType is "AD" or "MD" ? userType : null;
        if (normalizedType == null)
        {
            return null;
        }

        return await _context.TblUsers
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.Id == partnerId &&
                     x.Usertype == normalizedType &&
                     x.Status == "Active",
                cancellationToken);
    }

    private async Task<bool> VerifyOtpAsync(int partnerId, string? otp)
    {
        var dto = new OtpLoginLogDto
        {
            userid = partnerId.ToString(),
            usertype = "Retailer",
            otp = otp?.Trim() ?? string.Empty
        };

        return await _userRepository.LogOtpLoginAsync(dto);
    }

    private static bool IsPasswordValid(string? password)
    {
        if (string.IsNullOrWhiteSpace(password))
        {
            return false;
        }

        var regex = new Regex(
            @"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[@$!%*?&#^()_+=\-\[\]{};':""\\|,.<>\/]).{8,}$");

        return regex.IsMatch(password);
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

    private static ResponseSuccess BuildResponse(bool success, string message) =>
        new()
        {
            success = success,
            apitxnid = string.Empty,
            message = message,
            transactiondatetime = DateTime.Now.ToString(),
            txnid = string.Empty
        };
}

using InstantPay.Application.DTOs;
using InstantPay.Application.Interfaces;
using InstantPay.Infrastructure.Sql.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace InstantPay.Application.Services;

public class BeneficiaryService : IBeneficiaryService
{
    private readonly BeneficiaryDbContext _context;
    private readonly IOtpService _otpService;
    private readonly IMemoryCache _cache;
    private const int OtpExpirySeconds = 300; // 5 minutes
    private const int OtpResendCooldownSeconds = 30; // 30 seconds

    public BeneficiaryService(BeneficiaryDbContext context, IOtpService otpService, IMemoryCache cache)
    {
        _context = context;
        _otpService = otpService;
        _cache = cache;
    }

    public async Task<SaveBeneficiaryResponse> SaveBeneficiaryAsync(SaveBeneficiaryRequest request)
    {
        // Check for duplicate beneficiary
        var duplicate = await _context.Beneficiaries
            .AnyAsync(b => b.CustomerNumber == request.CustomerNumber 
                        && b.AccountNumber == request.AccountNumber 
                        && b.Ifsc == request.Ifsc);

        if (duplicate)
        {
            return new SaveBeneficiaryResponse
            {
                Success = false,
                Message = "Beneficiary already exists with same account number and IFSC"
            };
        }

        // Create new beneficiary
        var beneficiary = new Beneficiary
        {
            Status = true,
            Name = request.Name,
            AccountNumber = request.AccountNumber,
            BankName = request.BankName,
            Ifsc = request.Ifsc,
            CustomerNumber = request.CustomerNumber,
            CreatedOn = DateTime.UtcNow,
            UpdatedOn = DateTime.UtcNow
        };

        _context.Beneficiaries.Add(beneficiary);
        await _context.SaveChangesAsync();

        return new SaveBeneficiaryResponse
        {
            Success = true,
            Message = "Beneficiary saved successfully",
            Beneficiary = new BeneficiaryDto
            {
                Id = beneficiary.Id,
                Status = beneficiary.Status,
                Name = beneficiary.Name,
                AccountNumber = beneficiary.AccountNumber,
                BankName = beneficiary.BankName,
                Ifsc = beneficiary.Ifsc,
                CustomerNumber = beneficiary.CustomerNumber,
                CreatedOn = beneficiary.CreatedOn,
                UpdatedOn = beneficiary.UpdatedOn
            }
        };
    }

    public async Task<SendOtpResponse> SendOtpAsync(SendOtpRequest request)
    {
        var cacheKey = $"otp_last_sent_{request.CustomerNumber}";
        
        // Check rate limiting
        if (_cache.TryGetValue(cacheKey, out DateTime lastSentTime))
        {
            var timeSinceLastSend = DateTime.UtcNow - lastSentTime;
            if (timeSinceLastSend.TotalSeconds < OtpResendCooldownSeconds)
            {
                return new SendOtpResponse
                {
                    Success = false,
                    Message = $"Please wait {OtpResendCooldownSeconds - (int)timeSinceLastSend.TotalSeconds} seconds before requesting OTP again"
                };
            }
        }

        // Generate OTP
        var otp = new Random().Next(100000, 999999).ToString();
        var expiryTime = DateTime.UtcNow.AddSeconds(OtpExpirySeconds);

        // Store OTP in cache
        var otpCacheKey = $"otp_{request.CustomerNumber}";
        _cache.Set(otpCacheKey, otp, TimeSpan.FromSeconds(OtpExpirySeconds));
        
        // Store last sent time for rate limiting
        _cache.Set(cacheKey, DateTime.UtcNow, TimeSpan.FromSeconds(OtpResendCooldownSeconds));

        // Send OTP via SMS
        await _otpService.SendOtpAsync(request.CustomerNumber, otp);

        return new SendOtpResponse
        {
            Success = true,
            Message = "OTP sent successfully",
            OtpExpiryTime = expiryTime.ToString("yyyy-MM-dd HH:mm:ss")
        };
    }

    public async Task<SendOtpResponse> ResendOtpAsync(SendOtpRequest request)
    {
        // Resend uses the same logic as SendOtp with rate limiting
        return await SendOtpAsync(request);
    }

    public async Task<DeleteBeneficiaryResponse> DeleteBeneficiaryAsync(DeleteBeneficiaryRequest request)
    {
        // Verify OTP
        var otpCacheKey = $"otp_{request.CustomerNumber}";
        if (!_cache.TryGetValue(otpCacheKey, out string? storedOtp) || storedOtp != request.Otp)
        {
            return new DeleteBeneficiaryResponse
            {
                Success = false,
                Message = "Invalid or expired OTP"
            };
        }

        // Find beneficiary
        var beneficiary = await _context.Beneficiaries
            .FirstOrDefaultAsync(b => b.Id == request.BeneficiaryId 
                                    && b.CustomerNumber == request.CustomerNumber);

        if (beneficiary == null)
        {
            return new DeleteBeneficiaryResponse
            {
                Success = false,
                Message = "Beneficiary not found"
            };
        }

        // Delete beneficiary
        _context.Beneficiaries.Remove(beneficiary);
        await _context.SaveChangesAsync();

        // Clear OTP from cache after successful deletion
        _cache.Remove(otpCacheKey);

        return new DeleteBeneficiaryResponse
        {
            Success = true,
            Message = "Beneficiary deleted successfully"
        };
    }

    public async Task<GetBeneficiaryListResponse> GetBeneficiaryListAsync(GetBeneficiaryListRequest request)
    {
        var beneficiaries = await _context.Beneficiaries
            .Where(b => b.CustomerNumber == request.CustomerNumber)
            .Select(b => new BeneficiaryDto
            {
                Id = b.Id,
                Status = b.Status,
                Name = b.Name,
                AccountNumber = b.AccountNumber,
                BankName = b.BankName,
                Ifsc = b.Ifsc,
                CustomerNumber = b.CustomerNumber,
                CreatedOn = b.CreatedOn,
                UpdatedOn = b.UpdatedOn
            })
            .ToListAsync();

        return new GetBeneficiaryListResponse
        {
            Success = beneficiaries.Count > 0 ? true: false,
            Message = beneficiaries.Count > 0 ? "Beneficiaries found" : "No beneficiaries found",
            Beneficiaries = beneficiaries
        };
    }
}

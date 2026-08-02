using InstantPay.Application.DTOs;
using InstantPay.Application.Interfaces;
using InstantPay.Infrastructure.Sql.Entities;
using Microsoft.EntityFrameworkCore;

namespace InstantPay.Application.Services;

public class SenderService : ISenderService
{
    private readonly SenderDbContext _context;
    private readonly IOtpService _otpService;

    public SenderService(SenderDbContext context, IOtpService otpService)
    {
        _context = context;
        _otpService = otpService;
    }

    public async Task<SenderApiResponseDto> SenderLoginAsync(SenderLoginRequestDto request)
    {
        // Validate API Key
        if (request.APIKey != "CheckSender001")
        {
            return new SenderApiResponseDto
            {
                Status_Code = "0",
                Message = "Invalid API Key",
                Data = "Invalid API Key"
            };
        }

        var sender = await _context.Senders
            .FirstOrDefaultAsync(s => s.SenderMobile == request.SenderMobile);

        if (sender == null)
        {
            // Sender not registered
            return new SenderApiResponseDto
            {
                Status_Code = "0",
                Message = "Sender Not Available",
                Data = "Mobile number is not entered. Please enroll before login"
            };
        }

        if (!sender.IsKycVerified)
        {
            // Generate and send OTP
            await GenerateAndSendOtpAsync(sender);
            
            return new SenderApiResponseDto
            {
                Status_Code = "4",
                Message = "Verification is Pending, OTP has been sent at registered mobile number",
                Data = "Verification is Pending, OTP has been sent at registered mobile number"
            };
        }

        // Sender is verified, return success
        return new SenderApiResponseDto
        {
            Status_Code = "1",
            Message = "Sender Fetch Successful.",
            Data = new List<SenderResponseDto>
            {
                new SenderResponseDto
                {
                    first_name = sender.FirstName,
                    state = sender.State ?? ""
                }
            }
        };
    }

    public async Task<SenderApiResponseDto> SenderRegistrationAsync(SenderRegistrationRequestDto request)
    {
        // Validate API Key
        if (request.APIKey != "SenderReg001")
        {
            return new SenderApiResponseDto
            {
                Status_Code = "0",
                Message = "Invalid API Key",
                Data = "Invalid API Key"
            };
        }

        var sender = await _context.Senders
            .FirstOrDefaultAsync(s => s.SenderMobile == request.SenderMobile);

        if (sender != null)
        {
            // Update existing sender
            sender.FirstName = request.FirstName;
            sender.LastName = request.LastName ?? "";
            sender.Address = request.Address ?? "";
            sender.Pincode = request.Pincode ?? "";
            sender.IsKycVerified = false;
            sender.UpdatedOn = DateTime.UtcNow;
        }
        else
        {
            // Insert new sender
            sender = new Sender
            {
                SenderMobile = request.SenderMobile,
                FirstName = request.FirstName,
                LastName = request.LastName ?? "",
                Address = request.Address ?? "",
                Pincode = request.Pincode ?? "",
                State = "",
                IsKycVerified = false,
                CreatedOn = DateTime.UtcNow,
                UpdatedOn = DateTime.UtcNow
            };
            _context.Senders.Add(sender);
        }

        await _context.SaveChangesAsync();

        // Generate and send OTP
        await GenerateAndSendOtpAsync(sender);

        return new SenderApiResponseDto
        {
            Status_Code = "1",
            Message = "Verification is Pending, OTP has been sent at registered mobile number",
            Data = new List<SenderResponseDto>
            {
                new SenderResponseDto
                {
                    first_name = request.FirstName,
                    state = ""
                }
            }
        };
    }

    public async Task<SenderApiResponseDto> SenderEkycAsync(SenderEkycRequestDto request)
    {
        // Validate API Key
        if (request.APIKey != "SenderValidate001")
        {
            return new SenderApiResponseDto
            {
                Status_Code = "0",
                Message = "Invalid API Key",
                Data = "Invalid API Key"
            };
        }

        var sender = await _context.Senders
            .FirstOrDefaultAsync(s => s.SenderMobile == request.SenderMobile);

        if (sender == null)
        {
            return new SenderApiResponseDto
            {
                Status_Code = "0",
                Message = "Sender not found",
                Data = "Sender not found"
            };
        }

        if (string.IsNullOrEmpty(sender.Otp) || !sender.OtpExpiry.HasValue)
        {
            return new SenderApiResponseDto
            {
                Status_Code = "0",
                Message = "OTP not found or expired",
                Data = "OTP not found or expired"
            };
        }

        if (sender.OtpExpiry.Value < DateTime.UtcNow)
        {
            return new SenderApiResponseDto
            {
                Status_Code = "0",
                Message = "OTP expired",
                Data = "OTP expired"
            };
        }

        if (sender.Otp != request.OTP)
        {
            return new SenderApiResponseDto
            {
                Status_Code = "0",
                Message = "Invalid OTP",
                Data = "Invalid OTP"
            };
        }

        // Update sender as KYC verified
        sender.IsKycVerified = true;
        sender.State = request.State ?? "";
        sender.Otp = null;
        sender.OtpExpiry = null;
        sender.UpdatedOn = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return new SenderApiResponseDto
        {
            Status_Code = "1",
            Message = "Sender Succefully Created",
            Data = "User verified"
        };
    }

    private async Task GenerateAndSendOtpAsync(Sender sender)
    {
        // Generate 4-digit OTP
        var random = new Random();
        var otp = random.Next(1000, 9999).ToString();
        var otpExpiry = DateTime.UtcNow.AddMinutes(5);

        // Update OTP in database
        sender.Otp = otp;
        sender.OtpExpiry = otpExpiry;
        sender.UpdatedOn = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        // Send OTP via SMS service
        try
        {
            await _otpService.SendOtpAsync(sender.SenderMobile, otp);
            Console.WriteLine($"OTP sent to {sender.SenderMobile}: {otp} (Valid until {otpExpiry})");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to send OTP to {sender.SenderMobile}: {ex.Message}");
            // OTP is still saved in DB, user can proceed with manual OTP entry if needed
        }
    }
}

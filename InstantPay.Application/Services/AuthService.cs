using InstantPay.Application.Interfaces;
using InstantPay.Infrastructure.Security;
using InstantPay.Infrastructure.Sql.Entities;
using InstantPay.SharedKernel.Entity;
using InstantPay.SharedKernel.RequestPayload;
using InstantPay.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.RegularExpressions;
using BCrypt.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
namespace InstantPay.Application.Services
{
   
    public class AuthService : IAuthService
    {
        private readonly AppDbContext _context;
        private readonly IConfiguration _config;
        private readonly AesEncryptionService _aes;
        private readonly IOtpService _otpService;
        private readonly IEmailService _email;
        private readonly IMemoryCache _cache;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IUserRepository _userRepository;

        private const int MaxUnlockAttempts = 5;
        private static readonly TimeSpan AccountLockoutDuration = TimeSpan.FromMinutes(15);
        private static readonly int[] ProgressiveDelaysMs = { 0, 1_000, 2_000, 5_000, 10_000, 30_000 };
        private const int IpRateLimitMaxRequests = 20;
        private static readonly TimeSpan IpRateLimitWindow = TimeSpan.FromMinutes(15);

        public AuthService(AppDbContext context, IConfiguration config, AesEncryptionService aes, IOtpService otpService, IEmailService email, IMemoryCache cache, IHttpContextAccessor httpContextAccessor, IUserRepository userRepository)
        {
            _context = context;
            _config = config;
            _aes = aes;
            _otpService = otpService;
            _email = email;
            _cache = cache;
            _httpContextAccessor = httpContextAccessor;
            _userRepository = userRepository;
        }

        public async Task<UnlockResponseDto?> UnlockAsync(UnlockRequestDto request)
        {
            if (string.IsNullOrWhiteSpace(request.UserId)) return null;

            var clientIp = _httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            EnforceIpRateLimit(clientIp);

            var uid = request.UserId;

            if (request.UserType.Trim().ToLower() == "retailer")
            {
                var tblUser = await _context.TblUsers
                    .FirstOrDefaultAsync(u => u.Id.ToString() == uid && u.Status == "Active");

                if (tblUser == null) return null;

                CheckAccountLockout(tblUser.LockoutEnd);

                bool authSuccess = false;
                if (request.Method.Equals("mpin", StringComparison.OrdinalIgnoreCase))
                {
                    if (string.IsNullOrEmpty(tblUser.MPin))
                    {
                        await HandleRetailerFailedAttempt(tblUser, clientIp, request.Method);
                        return null;
                    }
                    authSuccess = tblUser.MPin == _aes.Decrypt(request.Value);
                }
                else if (request.Method.Equals("password", StringComparison.OrdinalIgnoreCase))
                {
                    authSuccess = tblUser.Password == _aes.Decrypt(request.Value);
                }
                else
                {
                    return null;
                }

                if (!authSuccess)
                {
                    await HandleRetailerFailedAttempt(tblUser, clientIp, request.Method);
                    return null;
                }

                tblUser.FailedUnlockAttempts = 0;
                tblUser.LockoutEnd = null;
                tblUser.IsUserLoggedInFromWeb = true;
                await _context.SaveChangesAsync();

                var user = new User
                {
                    Id = tblUser.Id,
                    Username = tblUser.Username,
                    Password = tblUser.Password,
                    Usertype = "Retailer",
                    Status = tblUser.Status,
                    Phoneno = tblUser.Phone
                };

                return new UnlockResponseDto
                {
                    Token = GenerateJwtToken(user),
                    Username = tblUser.Username ?? "",
                    Usertype = "Retailer",
                    message = "Unlocked",
                    Phoneno = tblUser.Phone ?? "",
                    OTP = "",
                    IsOtpRequired = false
                };
            }
            else
            {
                var tblUser = await _context.TblSuperadmins
                    .FirstOrDefaultAsync(u => u.Id.ToString() == uid && u.Status == "Active");

                if (tblUser == null) return null;

                CheckAccountLockout(tblUser.LockoutEnd);

                bool authSuccess = false;
                if (request.Method.Equals("mpin", StringComparison.OrdinalIgnoreCase))
                {
                    if (string.IsNullOrEmpty(tblUser.Mpin))
                    {
                        await HandleAdminFailedAttempt(tblUser, clientIp, request.Method);
                        return null;
                    }
                    authSuccess = tblUser.Mpin == _aes.Decrypt(request.Value);
                }
                else if (request.Method.Equals("password", StringComparison.OrdinalIgnoreCase))
                {
                    authSuccess = tblUser.Password == _aes.Decrypt(request.Value);
                }
                else
                {
                    return null;
                }

                if (!authSuccess)
                {
                    await HandleAdminFailedAttempt(tblUser, clientIp, request.Method);
                    return null;
                }

                tblUser.FailedUnlockAttempts = 0;
                tblUser.LockoutEnd = null;
                await _context.SaveChangesAsync();

                var user = new User
                {
                    Id = tblUser.Id,
                    Username = tblUser.Username,
                    Password = tblUser.Password,
                    Usertype = "SuperAdmin",
                    Status = tblUser.Status,
                    Phoneno = tblUser.Mobileno
                };

                return new UnlockResponseDto
                {
                    Token = GenerateJwtToken(user),
                    Username = tblUser.Username ?? "",
                    Usertype = "SuperAdmin",
                    message = "Unlocked",
                    Phoneno = tblUser.Mobileno ?? "",
                    OTP = "",
                    IsOtpRequired = false
                };
            }
        }

        private void EnforceIpRateLimit(string clientIp)
        {
            var key = $"ip_unlock_rate:{clientIp}";
            _cache.TryGetValue<int>(key, out var count);
            count++;
            _cache.Set(key, count, new MemoryCacheEntryOptions { SlidingExpiration = IpRateLimitWindow });

            if (count > IpRateLimitMaxRequests)
                throw new InvalidOperationException(
                    $"Too many unlock attempts from your network. Please wait {(int)IpRateLimitWindow.TotalMinutes} minutes before trying again.");
        }

        private static void CheckAccountLockout(DateTime? lockoutEnd)
        {
            if (lockoutEnd.HasValue && lockoutEnd.Value > DateTime.UtcNow)
            {
                var remaining = (int)Math.Ceiling((lockoutEnd.Value - DateTime.UtcNow).TotalMinutes);
                throw new InvalidOperationException(
                    $"Account is temporarily locked due to multiple failed attempts. Try again in {remaining} minute(s).");
            }
        }

        private async Task HandleRetailerFailedAttempt(TblUser user, string clientIp, string method)
        {
            user.FailedUnlockAttempts = (user.FailedUnlockAttempts ?? 0) + 1;
            if (user.FailedUnlockAttempts >= MaxUnlockAttempts)
                user.LockoutEnd = DateTime.UtcNow.Add(AccountLockoutDuration);

            _context.TblPasswordattmts.Add(new TblPasswordattmt
            {
                UserId = user.Id.ToString(),
                Ipaddress = clientIp,
                Password = $"FAILED_UNLOCK:{method.ToUpper()}|ATTEMPT:{user.FailedUnlockAttempts}",
                Reqdate = DateTime.UtcNow
            });
            await _context.SaveChangesAsync();
            await ApplyProgressiveDelay(user.FailedUnlockAttempts.Value);
        }

        private async Task HandleAdminFailedAttempt(TblSuperadmin admin, string clientIp, string method)
        {
            admin.FailedUnlockAttempts = (admin.FailedUnlockAttempts ?? 0) + 1;
            if (admin.FailedUnlockAttempts >= MaxUnlockAttempts)
                admin.LockoutEnd = DateTime.UtcNow.Add(AccountLockoutDuration);

            _context.TblPasswordattmts.Add(new TblPasswordattmt
            {
                UserId = admin.Id.ToString(),
                Ipaddress = clientIp,
                Password = $"FAILED_UNLOCK:{method.ToUpper()}|ATTEMPT:{admin.FailedUnlockAttempts}",
                Reqdate = DateTime.UtcNow
            });
            await _context.SaveChangesAsync();
            await ApplyProgressiveDelay(admin.FailedUnlockAttempts.Value);
        }

        private static async Task ApplyProgressiveDelay(int failedAttempts)
        {
            int idx = Math.Min(failedAttempts, ProgressiveDelaysMs.Length - 1);
            if (idx > 0)
                await Task.Delay(ProgressiveDelaysMs[idx]);
        }

        public string GenerateJwtToken(User user)
        {
            var claims = new[]
            {
                new Claim("userid", user.Id.ToString()),
                new Claim("username", user.Username ?? ""),
                new Claim("usertype", user.Usertype ?? "SuperAdmin")
            };

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"]!));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: _config["Jwt:Issuer"],
                audience: _config["Jwt:Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(30),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        public async Task<ResponseSuccess> UpdateUserInfo(UserRequestForCP request)
        {
            if (string.IsNullOrWhiteSpace(request?.UserId))
            {
                return BuildResponse(false, "Invalid User");
            }

            OtpLoginLogDto otpVerifyrequest = new OtpLoginLogDto();

            otpVerifyrequest.userid = request.UserId.Trim();
            otpVerifyrequest.usertype = "Retailer";
            otpVerifyrequest.otp = request?.OTP?.Trim()??"";

            var data = await _userRepository.LogOtpLoginAsync(otpVerifyrequest);
            if (data == false)
            {
                return BuildResponse(false, "Invalid OTP");
            }

            int userId = Convert.ToInt32(request.UserId);

            var userData = await _context.TblUsers
                .Where(x => x.Id == userId && x.Status.ToUpper() == "ACTIVE")
                .FirstOrDefaultAsync();

            if (request.Mode == "CPASS")
            {   
                userData.TxnPin = request.TxnPin;
                userData.MPin = request.MPin;
                await _context.SaveChangesAsync();
                return BuildResponse(true, "MPin & TxnPin Updated Successfully !");
            }

            if (!userData.Password.Trim().Equals(request.OldPassword?.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                return BuildResponse(false, "Old Password is not correct, Kindly Enter correct old Password !");
            }

            if (!request.NewPassword.Equals(request.ConfirmPassword))
            {
                return BuildResponse(false, "New Password And Confirm Password Should match !");
            }

            if (!IsPasswordValid(request.NewPassword))
            {
                return BuildResponse(false,
                    "Password must contain at least 1 uppercase, 1 lowercase, 1 numeric and 1 special character.");
            }

            userData.Password = request.ConfirmPassword;
            await _context.SaveChangesAsync();

            return BuildResponse(true, "Password Changed Successfully !");
        }

        public async Task<ResponseSuccess> ValidateUserInfoAndSentOTP(UserRequestForCP request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.UserId))
            {
                return BuildResponse(false, "Invalid User");
            }

            if (!int.TryParse(request.UserId, out int userId))
            {
                return BuildResponse(false, "Invalid User");
            }

            var userData = await _context.TblUsers
                .FirstOrDefaultAsync(x => x.Id == userId &&
                                         x.Status.Trim().ToUpper()=="ACTIVE");

            if (userData == null)
            {
                return BuildResponse(false, "User not found");
            }

            if (!string.Equals(userData.PanCard?.Trim(),
                               request.PANNo?.Trim(),
                               StringComparison.OrdinalIgnoreCase))
            {
                return BuildResponse(false, "Invalid PAN Card, Please Enter Registered PAN Card");
            }

            if (!string.Equals(userData.AadharCard?.Trim(),
                               request.AadharNo?.Trim(),
                               StringComparison.OrdinalIgnoreCase))
            {
                return BuildResponse(false, "Invalid Aadhar Number, Please Enter Registered Aadhar Number");
            }

            var otp = Random.Shared.Next(1000, 9999).ToString();
            var encryptedOtp = _aes.Encrypt(otp);
            // Store new OTP in database
            var loginOtp = new TblloginOtp
            {
                UserId = Convert.ToString(userId),
                IsUsed = false,
                OTP = otp,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddMinutes(10)
            };
            _context.TblloginOtps.Add(loginOtp);
            await _context.SaveChangesAsync();

            await _otpService.SendOtpAsync(userData.Phone?.Trim(), otp);

            return BuildResponse(true, encryptedOtp);
        }


        private bool IsPasswordValid(string password)
        {
            if (string.IsNullOrWhiteSpace(password))
                return false;

            var regex = new Regex(
                @"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[@$!%*?&#^()_+=\-\[\]{};':""\\|,.<>\/]).{8,}$"
            );

            return regex.IsMatch(password);
        }

        private ResponseSuccess BuildResponse(bool success, string message)
        {
            return new ResponseSuccess
            {
                success = success,
                apitxnid = "",
                message = message,
                transactiondatetime = DateTime.Now.ToString(),
                txnid = ""
            };
        }

        public async Task<ResponseSuccess> ForgetPassword(ForgetPasswordRequest request)
        {
            if(request.Mobile=="")
            {
                return BuildResponse(false, "Please Enter Registered Mobile Number");
            }

            var user = await _context.TblUsers
                .FirstOrDefaultAsync(x => x.Phone == request.Mobile && x.Status == "Active");

            if (user == null)
                return BuildResponse(false, "Mobile number not registered");

            if (!string.Equals(user.PanCard?.Trim(),
                               request.PANNumber?.Trim(),
                               StringComparison.OrdinalIgnoreCase))
            {
                return BuildResponse(false, "Invalid PAN Card, Please Enter Registered PAN Card");
            }

            if (!string.Equals(user.AadharCard?.Trim(),
                               request.AadharNumber?.Trim(),
                               StringComparison.OrdinalIgnoreCase))
            {
                return BuildResponse(false, "Invalid Aadhar Number, Please Enter Registered Aadhar Number");
            }

            var token = Guid.NewGuid().ToString("N");
            var otp = new Random().Next(1000, 9999).ToString();

            user.ResetToken = token;
            user.ResetTokenExpiry = DateTime.Now.AddMinutes(15);
            user.ResetOtpHash = BCrypt.Net.BCrypt.HashPassword(otp);
            user.ResetOtpExpiry = DateTime.Now.AddMinutes(5);
            user.ResetOtpAttempts = 0;
            user.LastOtpSentAt = DateTime.Now;

            await _context.SaveChangesAsync();

            var resetUrl =
                $"https://demo2.instantpayment.co.in/reset-password?token={token}";

            await _otpService.SendOtpAsync(
                user.Phone, otp
            );

            string data= await _email.SendOtpEmailAsync(user.EmailId.Trim(), BuildOtpEmailBody(user.Name.ToUpper(),otp, resetUrl));
            if(data!="1")
            {
                return BuildResponse(false, data);
            }

            return BuildResponse(true, "Reset Password Link Has been Shared on your Registered Email Id, Please reset your password with in 15 minutes!");
        }

        public async Task<ResponseSuccess> ExpirtCheckForForgetPassword(ResetPasswordRequest request)
        {

            var user = await _context.TblUsers.FirstOrDefaultAsync(x =>
                x.ResetToken == request.Token &&
                x.ResetTokenExpiry > DateTime.Now);

            if (user == null)
            {
                return BuildResponse(false, "Invalid or expired reset link");
            }

            return BuildResponse(true, "Valid");
        }

        private string BuildOtpEmailBody(string UserName, string otp, string resetUrl)
        {
            return $@"
                <!DOCTYPE html>
                <html lang='en'>
                <head>
                    <meta charset='UTF-8'>
                    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
                    <title>Password Reset - InstantPayment</title>
                </head>

                <body style='margin:0; padding:0; background-color:#f4f4f4; font-family:Arial, Helvetica, sans-serif;'>

                <table width='100%' cellpadding='0' cellspacing='0' style='background-color:#f4f4f4; padding:30px 0;'>
                    <tr>
                        <td align='center'>

                            <table width='600' cellpadding='0' cellspacing='0' style='background:#ffffff; border-radius:8px; overflow:hidden;'>

                                <!-- Header -->
                                <tr>
                                    <td style='background:#5e2f82; padding:20px; text-align:center;'>
                                        <img src='https://demo2.instantpayment.co.in/assets/images/logo_2.png'
                                             alt='InstantPayment'
                                             style='max-height:45px;' />
                                    </td>
                                </tr>

                                <!-- Body -->
                                <tr>
                                    <td style='padding:30px; color:#333333; font-size:15px; line-height:1.6;'>

                                        <p style='margin-top:0;'>Dear {UserName},</p>

                                        <p>
                                            We received a request to reset your <strong>InstantPayment</strong> account password.
                                            Please use the OTP below and click the reset link to proceed.
                                        </p>

                                        <!-- OTP Box -->
                                        <div style='margin:30px 0; text-align:center;'>
                                            <span style='display:inline-block;
                                                padding:15px 30px;
                                                font-size:26px;
                                                font-weight:bold;
                                                letter-spacing:6px;
                                                color:#5e2f82;
                                                border:2px dashed #5e2f82;
                                                border-radius:6px;'>
                                                {otp}
                                            </span>
                                        </div>

                                        <p style='text-align:center; margin-bottom:10px;'>
                                            <strong>OTP valid for 5 minutes</strong>
                                        </p>

                                        <!-- Reset Link -->
                                        <p>
                                            Please open the below link to reset your password and use the above otp:
                                        </p>

                                        <table align='center' cellpadding='0' cellspacing='0' role='presentation' style='margin:25px auto;'>
                                            <tr>
                                                <td align='center' bgcolor='#5e2f82' style='border-radius:6px;'>
                                                    <a href='{resetUrl}'
                                                       target='_blank'
                                                       style='
                                                           display:inline-block;
                                                           padding:14px 28px;
                                                           font-size:15px;
                                                           font-weight:bold;
                                                           font-family:Arial, Helvetica, sans-serif;
                                                           color:#ffffff;
                                                           text-decoration:none;
                                                           border-radius:6px;
                                                       '>
                                                        Open Link
                                                    </a>
                                                </td>
                                            </tr>
                                        </table>
                       
                                        <p style='font-size:13px; color:#555555;'>
                                            (This reset link is valid for <strong>15 minutes</strong>)
                                        </p>

                                        <p>
                                            For your security, please do not share your OTP or reset link with anyone.
                                            InstantPayment will never ask for this information.
                                        </p>

                                        <p>
                                            If you did not request a password reset, please ignore this email.
                                        </p>

                                        <p style='margin-bottom:0;'>
                                            Regards,<br/>
                                            <strong>InstantPayment Team</strong>
                                        </p>

                                    </td>
                                </tr>

                                <!-- Footer -->
                                <tr>
                                    <td style='background:#f8f8f8; padding:15px; text-align:center; font-size:12px; color:#777777;'>
                                        © {DateTime.Now.Year} InstantPayment. All rights reserved.
                                    </td>
                                </tr>

                            </table>

                        </td>
                    </tr>
                </table>

                </body>
                </html>";
        }


        public async Task<ResponseSuccess> ResetPassword(ResetPasswordRequest request)
        {
            var user = await _context.TblUsers.FirstOrDefaultAsync(x =>
                x.ResetToken == request.Token &&
                x.ResetTokenExpiry > DateTime.Now);

            if (user == null)
                return BuildResponse(false, "Invalid or expired reset link");

            if (user.ResetOtpExpiry < DateTime.Now)
                return BuildResponse(false, "OTP expired");

            if (!BCrypt.Net.BCrypt.Verify(request.Otp, user.ResetOtpHash))
                return BuildResponse(false, "Invalid OTP");

            if (!IsPasswordValid(request.NewPassword))
                return BuildResponse(false, "Password must contain uppercase, lowercase, number & special character");

            user.Password = request.NewPassword;
            user.ResetToken = null;
            user.ResetTokenExpiry = null;
            user.ResetOtpHash = null;
            user.ResetOtpExpiry = null;
            user.ResetOtpAttempts = 0;
            user.LastOtpSentAt = null;
            await _context.SaveChangesAsync();
            return BuildResponse(true, "Password reset successfully, Please Login with new Password!");
        }

       
        public async Task<ResponseSuccess> ResendResetOtp(ResendOtpRequest request)
        {
            var user = await _context.TblUsers.FirstOrDefaultAsync(x =>
                x.ResetToken == request.Token &&
                x.ResetTokenExpiry > DateTime.Now);

            if (user == null)
                return BuildResponse(false, "Invalid or expired reset link");

            if (user.ResetOtpAttempts >= 3)
                return BuildResponse(false, "Maximum OTP resend attempts exceeded");

            if (user.LastOtpSentAt.HasValue &&
                (DateTime.Now - user.LastOtpSentAt.Value).TotalSeconds < 30)
                return BuildResponse(false, "Please wait 30 seconds before resending OTP");

            var otp = new Random().Next(1000, 9999).ToString();

            user.ResetOtpHash = BCrypt.Net.BCrypt.HashPassword(otp);
            user.ResetOtpExpiry = DateTime.Now.AddMinutes(5);
            user.LastOtpSentAt = DateTime.Now;
            user.ResetOtpAttempts += 1;

            await _context.SaveChangesAsync();

            await _otpService.SendOtpAsync(
                user.Phone, otp
            );

            return BuildResponse(true, "OTP resent successfully");
        }


    }

}

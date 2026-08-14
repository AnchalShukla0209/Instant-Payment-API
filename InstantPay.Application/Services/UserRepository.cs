using InstantPay.Application.Interfaces;
using InstantPay.Infrastructure.Sql.Entities;
using InstantPay.SharedKernel.Entity;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace InstantPay.Application.Services
{
    public class UserRepository : IUserRepository
    {
        private readonly AppDbContext _context;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IOtpService _otpService;

        public UserRepository(AppDbContext context, IHttpContextAccessor httpContextAccessor, IOtpService otpService)
        {
            _context = context;
            _httpContextAccessor = httpContextAccessor;
            _otpService = otpService;
        }

        public async Task<User?> GetByUsernameAndPasswordAsync(string username, string password)
        {
            var tblUser = await _context.TblUsers
                .FirstOrDefaultAsync(u => u.Username == username && u.Password == password && u.Status == "Active");

            if (tblUser == null)
            {
                var tblSUser = await _context.TblSuperadmins
               .FirstOrDefaultAsync(u => u.Username == username && u.Password == password && u.Status == "Active");

                if (tblSUser == null) return null;

                var todays = DateTime.Today;
                var IsOtpRequiredAsyncs = true;

                if (IsOtpRequiredAsyncs)
                {
                    string otp = new Random().Next(1000, 9999).ToString();
                    await _otpService.SendOtpAsync(tblSUser.Mobileno, otp);

                    // Store OTP in database
                    var loginOtp = new TblloginOtp
                    {
                        UserId = Convert.ToString(tblSUser.Id),
                        IsUsed= false,
                        OTP = otp,
                        CreatedAt = DateTime.UtcNow,
                        ExpiresAt = DateTime.UtcNow.AddMinutes(10)
                    };
                    _context.TblloginOtps.Add(loginOtp);
                    await _context.SaveChangesAsync();

                    return new User
                    {
                        Id = tblSUser.Id,
                        Username = tblSUser.Username,
                        Password = tblSUser.Password,
                        Status = tblSUser.Status,
                        Usertype = "SuperAdmin",
                        IsOtpRequired = IsOtpRequiredAsyncs,
                        OTP = "",
                        Phoneno= tblSUser.Mobileno
                    };
                }
                return new User
                {
                    Id = tblSUser.Id,
                    Username = tblSUser.Username,
                    Password = tblSUser.Password,
                    Status = tblSUser.Status,
                    Usertype = "SuperAdmin",
                    IsOtpRequired = IsOtpRequiredAsyncs,
                    OTP = "",
                    Phoneno = tblSUser.Mobileno
                };
            }

            var today = DateTime.Today;
            var IsOtpRequiredAsync = !await _context.Tblloginlogs
                .Where(x =>
                    x.UserId == Convert.ToString(tblUser.Id) &&
                    x.Usertype == "Retailer" &&
                    x.Ipaddress == GetIpAddress() &&
                    x.Macaddress == Convert.ToString(_otpService.GetMacAddress()) &&
                    x.LoginTime >= today && x.LoginTime <= today.AddDays(1))
                .AnyAsync();

            if (IsOtpRequiredAsync)
            {
                string otp = new Random().Next(1000, 9999).ToString();
                await _otpService.SendOtpAsync(tblUser.Phone, otp);

                // Store OTP in database
                var loginOtp = new TblloginOtp
                {
                    UserId = Convert.ToString(tblUser.Id),
                    IsUsed = false,
                    OTP = otp,
                    CreatedAt = DateTime.UtcNow,
                    ExpiresAt = DateTime.UtcNow.AddMinutes(10)
                };
                _context.TblloginOtps.Add(loginOtp);
                await _context.SaveChangesAsync();

                return new User
                {
                    Id = tblUser.Id,
                    Username = tblUser.Username,
                    Password = tblUser.Password,
                    Status = tblUser.Status,
                    Usertype = "Retailer",
                    IsOtpRequired = IsOtpRequiredAsync,
                    OTP = "",
                    Phoneno = tblUser.Phone
                };
            }
            return new User
            {
                Id = tblUser.Id,
                Username = tblUser.Username,
                Password = tblUser.Password,
                Status = tblUser.Status,
                Usertype = "Retailer",
                IsOtpRequired = IsOtpRequiredAsync,
                OTP = "",
                Phoneno = tblUser.Phone
            };

        }

        public async Task<TblUser?> GetUserByIdAsync(int userId) =>
        await _context.TblUsers.FirstOrDefaultAsync(x => x.Id == userId);

        public async Task<TblSuperadmin?> GetSuperAdminByIdAsync(int userId) =>
        await _context.TblSuperadmins.FirstOrDefaultAsync(x => x.Id == userId);

        private string GetIpAddress()
        {
            return _httpContextAccessor.HttpContext?.Connection?.RemoteIpAddress?.ToString() ?? "0.0.0.0";
        }


        public async Task<bool> LogOtpLoginAsync(OtpLoginLogDto dto)
        {
            try
            {
                // Verify the OTP against stored value
                var storedOtp = await _context.TblloginOtps
                    .Where(x =>
                        x.UserId == Convert.ToString(dto.userid) &&
                        x.ExpiresAt >= DateTime.UtcNow && x.IsUsed== false)
                    .OrderByDescending(x => x.CreatedAt)
                    .FirstOrDefaultAsync();

                if (storedOtp == null || storedOtp.OTP != dto.otp)
                {
                    return false;
                }

                // OTP is valid, create login log
                var log = new Tblloginlog
                {
                    Usertype = dto.usertype,
                    UserId = Convert.ToString(dto.userid),
                    Macaddress = Convert.ToString(_otpService.GetMacAddress()),
                    Ipaddress = GetIpAddress(),
                    LoginTime = DateTime.UtcNow,
                    OTPVerified = true
                };

                _context.Tblloginlogs.Add(log);

                // Remove used OTP
                _context.TblloginOtps.Remove(storedOtp);

                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                return false;
            }

        }

        public void InsertData(string userId)
        {
                Tblloginlog log = new Tblloginlog
                {
                    Usertype = "USER",
                    UserId = userId,
                    Macaddress = "aaa",
                    Ipaddress = _httpContextAccessor.HttpContext.Connection.RemoteIpAddress?.ToString(),
                    LoginTime = DateTime.Now
                };

            _context.Tblloginlogs.Add(log);
            _context.SaveChanges();
        }


        public async Task<string> ResendOTPAsyncn(OtpLoginLogDto dto)
        {
            try
            {
                string MobNo = "";
                if (dto.usertype == "Retailer")
                {
                    var tblUser = await _context.TblUsers
                    .FirstOrDefaultAsync(u => u.Id == Convert.ToInt32(dto.userid) && u.Status == "Active");
                    if (tblUser == null)
                    {
                        return "";
                    }
                    MobNo = tblUser.Phone.Trim();
                }
                else if (dto.usertype == "SuperAdmin")
                {
                    var tblUser = await _context.TblSuperadmins
                    .FirstOrDefaultAsync(u => u.Id == Convert.ToInt32(dto.userid) && u.Status == "Active");
                    if (tblUser == null)
                    {
                        return "";
                    }
                    MobNo = tblUser.Mobileno.Trim();
                }
                string otp = new Random().Next(1000, 9999).ToString();
                await _otpService.SendOtpAsync(MobNo, otp);

                // Store new OTP in database
                var loginOtp = new TblloginOtp
                {
                    UserId = Convert.ToString(dto.userid),
                    IsUsed = false,
                    OTP = otp,
                    CreatedAt = DateTime.UtcNow,
                    ExpiresAt = DateTime.UtcNow.AddMinutes(10)
                };
                _context.TblloginOtps.Add(loginOtp);
                await _context.SaveChangesAsync();

                return "success"; // Return empty string as OTP should not be exposed
            }
            catch (Exception ex)
            {
                return "";
            }

        }


        public async Task UpdateLoginStatusAsync(int userId, string platform, bool isLoggedIn)
        {
            var user = await _context.TblUsers.FirstOrDefaultAsync(x => x.Id == userId);
            if (user == null) return;

            if (string.Equals(platform, "web", StringComparison.OrdinalIgnoreCase))
                user.IsUserLoggedInFromWeb = isLoggedIn;
            else
                user.IsUserLoggedInFromApk = isLoggedIn;

            _context.TblUsers.Update(user);
            await _context.SaveChangesAsync();
        }

        public async Task<ServiceRightsData> GetUserRightsInfo(int Id)
        {
            try
            {
                var tblUser = await _context.TblUsers
        .FirstOrDefaultAsync(u => u.Id == Convert.ToInt32(Id) && u.Status == "Active");

                if (tblUser == null)
                {
                    return null;
                }

                var servData = new ServiceRightsData
                {
                    aeps = tblUser.Aeps,
                    microatm = tblUser.MicroAtm,
                    moneytransfer = tblUser.MoneyTransfer,
                    billpayment = tblUser.BillPayment,
                    mobilerecharge = tblUser.MobileRecharge,
                    razorpaypayment = tblUser.RazorpayPayment,
                    settlement = tblUser.Settlement
                };

                return servData;
            }
            catch (Exception ex)
            {
                return null;
            }

        }
    }
}

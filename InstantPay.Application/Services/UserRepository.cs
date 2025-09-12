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
                var IsOtpRequiredAsyncs = !await _context.Tblloginlogs
                    .Where(x =>
                        x.UserId == Convert.ToString(tblSUser.Id) &&
                        x.Usertype == "SuperAdmin" &&
                        x.Ipaddress == GetIpAddress() &&
                        x.Macaddress == Convert.ToString(_otpService.GetMacAddress()) &&
                        x.LoginTime >= todays && x.LoginTime <= todays.AddDays(1))
                    .AnyAsync();

                if (IsOtpRequiredAsyncs)
                {
                    string otp = new Random().Next(1000, 9999).ToString();
                    await _otpService.SendOtpAsync(tblSUser.Mobileno, otp);
                    return new User
                    {
                        Id = tblSUser.Id,
                        Username = tblSUser.Username,
                        Password = tblSUser.Password,
                        Status = tblSUser.Status,
                        Usertype = "SuperAdmin",
                        IsOtpRequired = IsOtpRequiredAsyncs,
                        OTP = otp
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
                    OTP = ""
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
                return new User
                {
                    Id = tblUser.Id,
                    Username = tblUser.Username,
                    Password = tblUser.Password,
                    Status = tblUser.Status,
                    Usertype = "Retailer",
                    IsOtpRequired = IsOtpRequiredAsync,
                    OTP = otp
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
                OTP = ""
            };

        }

        public async Task<TblUser?> GetUserByIdAsync(int userId) =>
        await _context.TblUsers.FirstOrDefaultAsync(x => x.Id == userId);

        private string GetIpAddress()
        {
            return _httpContextAccessor.HttpContext?.Connection?.RemoteIpAddress?.ToString() ?? "0.0.0.0";
        }


        public async Task<bool> LogOtpLoginAsync(OtpLoginLogDto dto)
        {
            try
            {
                var log = new Tblloginlog
                {
                    Usertype = dto.usertype,
                    UserId = Convert.ToString(dto.userid),
                    Macaddress = Convert.ToString(_otpService.GetMacAddress()),
                    Ipaddress = GetIpAddress(),
                    LoginTime = DateTime.UtcNow
                };

                _context.Tblloginlogs.Add(log);
                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                return false;
            }

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
                return otp;
            }
            catch (Exception ex)
            {
                return "";
            }

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
                    mobilerecharge = tblUser.MobileRecharge
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

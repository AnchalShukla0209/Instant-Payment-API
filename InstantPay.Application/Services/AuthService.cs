using InstantPay.Application.Interfaces;
using InstantPay.Infrastructure.Security;
using InstantPay.Infrastructure.Sql.Entities;
using InstantPay.SharedKernel.Entity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace InstantPay.Application.Services
{
   
    public class AuthService : IAuthService
    {
        private readonly AppDbContext _context;
        private readonly IConfiguration _config;
        private readonly AesEncryptionService _aes;

        public AuthService(AppDbContext context, IConfiguration config, AesEncryptionService aes)
        {
            _context = context;
            _config = config;
            _aes = aes;
        }

        public async Task<UnlockResponseDto?> UnlockAsync(UnlockRequestDto request)
        {
            if (string.IsNullOrWhiteSpace(request.UserId)) return null;

          
            var uid = request.UserId;

            if (request.UserType.Trim().ToLower() == "retailer")
            {
                var tblUser = await _context.TblUsers
                    .FirstOrDefaultAsync(u => u.Id.ToString() == uid && u.Status == "Active");

                if (tblUser == null) return null;

                if (request.Method.Equals("mpin", StringComparison.OrdinalIgnoreCase))
                {

                    if (string.IsNullOrEmpty(_aes.Encrypt(tblUser.MPin))) return null;

                    if (tblUser.MPin != _aes.Decrypt(request.Value))
                        return null;
                }
                else if (request.Method.Equals("password", StringComparison.OrdinalIgnoreCase))
                {
                    if (tblUser.Password != _aes.Decrypt(request.Value))
                        return null;
                }
                else
                {
                    return null;
                }

                var user = new User
                {
                    Id = tblUser.Id,
                    Username = tblUser.Username,
                    Password = tblUser.Password,
                    Usertype = "Retailer",
                    Status = tblUser.Status,
                    Phoneno = tblUser.Phone
                };

                var token = GenerateJwtToken(user);

                return new UnlockResponseDto
                {
                    Token = token,
                    Username = tblUser.Username ?? "",
                    Usertype = "Retailer",
                    message = "Unlocked",
                    Phoneno = tblUser.Phone ?? "",
                    OTP="",
                    IsOtpRequired = false
                    
                };
            }
            else
            {
                var tblUser = await _context.TblSuperadmins
                    .FirstOrDefaultAsync(u => u.Id.ToString() == uid && u.Status == "Active");

                if (tblUser == null) return null;

                if (request.Method.Equals("mpin", StringComparison.OrdinalIgnoreCase))
                {

                    if (string.IsNullOrEmpty(_aes.Encrypt(tblUser.Mpin))) return null;

                    if (tblUser.Mpin != _aes.Encrypt(request.Value))
                        return null;
                }
                else if (request.Method.Equals("password", StringComparison.OrdinalIgnoreCase))
                {
                    if (tblUser.Password != _aes.Encrypt(request.Value))
                        return null;
                }
                else
                {
                    return null;
                }

                var user = new User
                {
                    Id = tblUser.Id,
                    Username = tblUser.Username,
                    Password = tblUser.Password,
                    Usertype = "SuperAdmin",
                    Status = tblUser.Status,
                    Phoneno = tblUser.Mobileno
                };

                var token = GenerateJwtToken(user);

                return new UnlockResponseDto
                {
                    Token = token,
                    Username = tblUser.Username ?? "",
                    Usertype = "SuperAdmin",
                    message = "Unlocked",
                    Phoneno = tblUser.Mobileno ?? "",
                    OTP = "",
                    IsOtpRequired = false
                };
            }

                
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
    }

}

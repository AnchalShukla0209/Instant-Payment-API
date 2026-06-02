using Azure.Core;
using InstantPay.Application.DTOs;
using InstantPay.Application.Interfaces;
using InstantPay.SharedKernel.Entity;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

public class LoginService : ILoginService
{
    private readonly IUserRepository _userRepository;
    private readonly IConfiguration _config;

    public LoginService(IUserRepository userRepository, IConfiguration config)
    {
        _userRepository = userRepository;
        _config = config;
    }

    public async Task<LoginResponseDto?> LoginAsync(LoginRequestDto request, string platform)
    {
        var user = await _userRepository.GetByUsernameAndPasswordAsync(request.username, request.password);
        if (user == null)
        {
            return null;
        }

        var claims = new[]
        {
            new Claim("userid", user.Id.ToString()),
            new Claim("username", user.Username ?? ""),
            new Claim("usertype", user.Usertype ?? "SuperAdmin"),
            new Claim("mobileno", user.Phoneno??"")
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

        if (!(user.IsOtpRequired ?? false) && user.Usertype == "Retailer")
        {
            await _userRepository.UpdateLoginStatusAsync(user.Id, platform, true);
        }

        return new LoginResponseDto
        {
            Username = user.Username ?? "",
            Usertype = user.Usertype ?? "",
            IsOtpRequired = user.IsOtpRequired ?? false,
            Token = new JwtSecurityTokenHandler().WriteToken(token),
            messaege="",
            Phoneno= user.Phoneno,
            userid = Convert.ToString(user.Id)
        };
    }


    public async Task<LoginResponseDto?> VerifyOTP(OtpLoginLogDto request, string platform)
    {
        var data = await _userRepository.LogOtpLoginAsync(request);
        if (data == false)
        {
            return null;
        }

        string username = "";
        string usertype = "";
        string mobileno = "";
        int userId = 0;

        if (request.usertype == "SuperAdmin")
        {
            var superAdmin = await _userRepository.GetSuperAdminByIdAsync(Convert.ToInt32(request.userid));
            if (superAdmin == null)
            {
                return null;
            }
            username = superAdmin.Username ?? "";
            usertype = "SuperAdmin";
            mobileno = superAdmin.Mobileno ?? "";
            userId = superAdmin.Id;
        }
        else if (request.usertype == "Retailer")
        {
            var tblUser = await _userRepository.GetUserByIdAsync(Convert.ToInt32(request.userid));
            if (tblUser == null)
            {
                return null;
            }
            username = tblUser.Username ?? "";
            usertype = tblUser.Usertype ?? "";
            mobileno = tblUser.Phone ?? "";
            userId = tblUser.Id;
        }
        else
        {
            return null;
        }

        var claims = new[]
        {
            new Claim("userid", userId.ToString()),
            new Claim("username", username),
            new Claim("usertype", usertype),
            new Claim("mobileno", mobileno)
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

        if (request.usertype == "Retailer")
        {
            await _userRepository.UpdateLoginStatusAsync(userId, platform, true);
        }

        return new LoginResponseDto
        {
            Username = username,
            Usertype = usertype,
            IsOtpRequired = false,
            Token = new JwtSecurityTokenHandler().WriteToken(token),
            messaege = "OTP Verified Successfully",
            Phoneno = mobileno,
            userid = Convert.ToString(userId)
        };
    }

    public async Task<LoginResponseDto?> ResendOTP(OtpLoginLogDto request)
    {
        var data = await _userRepository.ResendOTPAsyncn(request);
        if (data != "")
        {
            return new LoginResponseDto
            {
                Username = "",
                Usertype = "",
                IsOtpRequired = true,
                Token = "",
                messaege = "OTP Sent Successfully"
            };
        }
        return new LoginResponseDto
        {
            Username = "",
            Usertype = "",
            IsOtpRequired = true,
            Token = "",
            messaege = "OTP Resend Failed"
        };
    }

    
    public async Task<bool> LogoutAsync(int userId, string platform)
    {
        await _userRepository.UpdateLoginStatusAsync(userId, platform, false);
        return true;
    }

    public async Task<ServiceRightsData> GetUserRightsInfoDet(int Id)
    {
        try
        {
            var data = await _userRepository.GetUserRightsInfo(Id);
            return data;
        }
        catch (Exception ex)
        {
            return null;
        }

    }

}

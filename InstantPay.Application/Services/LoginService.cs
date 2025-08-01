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

    public async Task<LoginResponseDto?> LoginAsync(LoginRequestDto request)
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
            new Claim("usertype", user.Usertype ?? "SuperAdmin")
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"]!));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _config["Jwt:Issuer"],
            audience: _config["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: creds
        );

        return new LoginResponseDto
        {
            Username = user.Username ?? "",
            Usertype = user.Usertype ?? "",
            OTP = user.OTP ?? "",
            IsOtpRequired = user.IsOtpRequired ?? false,
            Token = new JwtSecurityTokenHandler().WriteToken(token),
            messaege=""
        };
    }


    public async Task<OTPSuccessResponse?> VerifyOTP(OtpLoginLogDto request)
    {
        var data = await _userRepository.LogOtpLoginAsync(request);
        if (data == false)
        {
            return new OTPSuccessResponse
            {
                success= false,
                message="OTP Logging Failed"
            };
        }
        return new OTPSuccessResponse
        {
            success = true,
            message = "success"
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
                OTP = data,
                IsOtpRequired = true,
                Token = "",
                messaege = "OTP Sent Successfully"
            };
        }
        return new LoginResponseDto
        {
            Username = "",
            Usertype = "",
            OTP = data,
            IsOtpRequired = true,
            Token = "",
            messaege = "OTP Resend Failed"
        };
    }

}

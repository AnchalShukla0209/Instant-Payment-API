using InstantPay.Application.DTOs;
using InstantPay.Application.Interfaces;
using InstantPay.Infrastructure.Security;
using InstantPay.SharedKernel.Entity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace InstantPay.API.Controller
{
    [ApiController]
    [Route("api/[controller]")]
    public class LoginController : ControllerBase
    {
        private readonly ILoginService _loginService;
        private readonly AesEncryptionService _aes;
        public LoginController(ILoginService loginService, AesEncryptionService aes)
        {
            _aes = aes;
            _loginService = loginService;
        }
        [AllowAnonymous]
        [HttpPost]
        public async Task<IActionResult> Login(EncryptedRequest request)
        {
            var decryptedJson = _aes.Decrypt(request.Data);
            var login = JsonSerializer.Deserialize<LoginRequestDto>(decryptedJson);
            var EncPassword = _aes.Encrypt(_aes.Encrypt(login.password));
            login.password = EncPassword;
            var response = await _loginService.LoginAsync(login);
            if (response == null)
                return Unauthorized(new { message = "Invalid username or password" });

            var responseJson = JsonSerializer.Serialize(response);
            var encryptedResponse = _aes.Encrypt(responseJson);
            return Ok(new { data = encryptedResponse });
        }

        [AllowAnonymous]
        [HttpPost("verifyotp")]
        public async Task<IActionResult> VerifyOTP(EncryptedRequest request)
        {
            var decryptedJson = _aes.Decrypt(request.Data);
            var login = JsonSerializer.Deserialize<OtpLoginLogDto>(decryptedJson);
            var response = await _loginService.VerifyOTP(login);
            var responseJson = JsonSerializer.Serialize(response);
            var encryptedResponse = _aes.Encrypt(responseJson);
            return Ok(new { data = encryptedResponse });
        }

        [AllowAnonymous]
        [HttpPost("resendotp")]
        public async Task<IActionResult> ResendOTP(EncryptedRequest request)
        {
            var decryptedJson = _aes.Decrypt(request.Data);
            var login = JsonSerializer.Deserialize<OtpLoginLogDto>(decryptedJson);
            var response = await _loginService.ResendOTP(login);
            var responseJson = JsonSerializer.Serialize(response);
            var encryptedResponse = _aes.Encrypt(responseJson);
            return Ok(new { data = encryptedResponse });
        }

        [HttpGet("get-rightsinfo")]
        public async Task<IActionResult> GetRightsinfo(int Id)
        {
            var response = await _loginService.GetUserRightsInfoDet(Id);
            return Ok(new { data = response });
        }

    }
}

using InstantPay.Application.DTOs;
using InstantPay.Application.Interfaces;
using InstantPay.Application.Services;
using InstantPay.Infrastructure.Security;
using InstantPay.SharedKernel.Entity;
using InstantPay.SharedKernel.RequestPayload;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace InstantPay.API.Controller
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;
        public AuthController(IAuthService loginService)
        {
            _authService = loginService;
        }
        [AllowAnonymous]
        [HttpPost("unlock")]
        public async Task<IActionResult> Unlock([FromBody] UnlockRequestDto request)
        {
            if (request == null) return BadRequest("Invalid request");

            try
            {
                var result = await _authService.UnlockAsync(request);
                if (result == null) return StatusCode(222, new { message = "Invalid credentials" });
                return Ok(result);
            }
            catch (InvalidOperationException ex) when (ex.Message.StartsWith("Account is temporarily locked"))
            {
                return StatusCode(423, new { message = ex.Message });
            }
            catch (InvalidOperationException ex) when (ex.Message.StartsWith("Too many unlock attempts"))
            {
                return StatusCode(429, new { message = ex.Message });
            }
        }

        [AllowAnonymous]
        [HttpPost("UpdateUserInfo")]
        public async Task<IActionResult> UpdateUserInfo([FromBody] UserRequestForCP request)
        {
            if (request == null) return BadRequest("Invalid request");
            var result = await _authService.UpdateUserInfo(request);
            if (result == null) return BadRequest(new { message = "Invalid credentials" });
            return Ok(result);
        }

        [HttpPost("forget-password")]
        public async Task<IActionResult> ForgetPassword([FromBody] ForgetPasswordRequest request)
        {
            var result = await _authService.ForgetPassword(request);
            if (result == null) return BadRequest(new { message = "Invalid credentials" });
            return Ok(result);
        }

        [HttpPost("expiry-forget-password")]
        public async Task<IActionResult> ExpiryForgetPassword([FromBody] ResetPasswordRequest request)
        {
            var result = await _authService.ExpirtCheckForForgetPassword(request);
            if (result == null) return BadRequest(new { message = "Invalid credentials" });
            return Ok(result);
        }

        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request)
        {
            var result = await _authService.ResetPassword(request);
            if (result == null) return BadRequest(new { message = "Invalid credentials" });
            return Ok(result);
        }

        [HttpPost("resend-reset-otp")]
        public async Task<IActionResult> ResendResetOtp([FromBody] ResendOtpRequest request)
        {
            var result = await _authService.ResendResetOtp(request);
            if (result == null) return BadRequest(new { message = "Invalid credentials" });
            return Ok(result);
        }

        [HttpPost("ValidateUserInfoAndSentOTP")]
        public async Task<IActionResult> ValidateUserInfoAndSentOTP([FromBody] UserRequestForCP request)
        {
            var result = await _authService.ValidateUserInfoAndSentOTP(request);
            if (result == null) return BadRequest(new { message = "Invalid credentials" });
            return Ok(result);
        }

        



    }
}

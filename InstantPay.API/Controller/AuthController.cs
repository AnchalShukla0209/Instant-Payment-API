using InstantPay.Application.DTOs;
using InstantPay.Application.Interfaces;
using InstantPay.Application.Services;
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

            var result = await _authService.UnlockAsync(request);
            if (result == null) return BadRequest(new { message = "Invalid credentials" });

            return Ok(result);
        }

    }
}

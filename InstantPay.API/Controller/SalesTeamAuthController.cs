using InstantPay.Application.DTOs;
using InstantPay.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace InstantPay.API.Controller;

[ApiController]
[AllowAnonymous]
[Route("api/v1/sales-team/auth")]
[Produces("application/json")]
public sealed class SalesTeamAuthController : ControllerBase
{
    private readonly IDistributorAuthService _authService;
    public SalesTeamAuthController(IDistributorAuthService authService) => _authService = authService;

    [HttpPost("login")]
    [EnableRateLimiting("distributor-login")]
    public async Task<IActionResult> Login([FromBody] DistributorLoginRequest request, CancellationToken cancellationToken)
    {
        SetNoStoreHeaders();
        return ToActionResult(await _authService.LoginAsync(
            request, "ST", HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown", cancellationToken));
    }

    [HttpPost("verify-otp")]
    [EnableRateLimiting("distributor-otp")]
    public async Task<IActionResult> VerifyOtp([FromBody] DistributorOtpRequest request, CancellationToken cancellationToken)
    {
        SetNoStoreHeaders();
        return ToActionResult(await _authService.VerifyOtpAsync(
            request, "ST", HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown", cancellationToken));
    }

    private IActionResult ToActionResult<T>(DistributorAuthResult<T> result) => result.Succeeded
        ? Ok(result.Data)
        : Problem(statusCode: result.StatusCode, title: result.Message,
            type: $"https://api.instantpayment.co.in/problems/{result.Code.ToLowerInvariant()}",
            extensions: new Dictionary<string, object?> { ["code"] = result.Code });

    private void SetNoStoreHeaders()
    {
        Response.Headers.CacheControl = "no-store, no-cache, must-revalidate";
        Response.Headers.Pragma = "no-cache";
    }
}

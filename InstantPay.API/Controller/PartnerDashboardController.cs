using System.Security.Claims;
using InstantPay.Application.DTOs;
using InstantPay.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace InstantPay.API.Controller;

[ApiController]
[Authorize(Policy = "PartnerDashboard")]
[EnableRateLimiting("partner-dashboard")]
[Route("api/v1/partner/dashboard")]
[Produces("application/json")]
public sealed class PartnerDashboardController : ControllerBase
{
    private readonly IPartnerDashboardService _dashboardService;
    private readonly ILogger<PartnerDashboardController> _logger;

    public PartnerDashboardController(
        IPartnerDashboardService dashboardService,
        ILogger<PartnerDashboardController> logger)
    {
        _dashboardService = dashboardService;
        _logger = logger;
    }

    [HttpGet]
    [ProducesResponseType<PartnerDashboardResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDashboard(
        [FromQuery] int days = 7,
        CancellationToken cancellationToken = default)
    {
        SetNoStoreHeaders();
        if (!TryGetPartnerIdentity(out var partnerId, out var userType))
        {
            return Unauthorized();
        }

        try
        {
            return Ok(await _dashboardService.GetDashboardAsync(
                partnerId,
                userType,
                days,
                cancellationToken));
        }
        catch (UnauthorizedAccessException exception)
        {
            _logger.LogWarning(
                exception,
                "Rejected partner dashboard access for user {PartnerId}.",
                partnerId);
            return Forbid();
        }
    }

    [HttpGet("wallet")]
    [ProducesResponseType<PartnerWalletResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetWallet(
        CancellationToken cancellationToken)
    {
        SetNoStoreHeaders();
        if (!TryGetPartnerIdentity(out var partnerId, out _))
        {
            return Unauthorized();
        }

        return Ok(await _dashboardService.GetWalletAsync(
            partnerId,
            cancellationToken));
    }

    private bool TryGetPartnerIdentity(out int partnerId, out string userType)
    {
        var idClaim = User.FindFirstValue("userid") ??
                      User.FindFirstValue(ClaimTypes.NameIdentifier);
        userType = User.FindFirstValue("usertype") ?? string.Empty;
        return int.TryParse(idClaim, out partnerId) &&
               partnerId > 0 &&
               (userType == "AD" || userType == "MD");
    }

    private void SetNoStoreHeaders()
    {
        Response.Headers.CacheControl = "no-store, no-cache, must-revalidate";
        Response.Headers.Pragma = "no-cache";
    }
}

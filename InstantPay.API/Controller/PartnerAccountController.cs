using System.Security.Claims;
using InstantPay.Application.DTOs;
using InstantPay.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InstantPay.API.Controller;

/// <summary>
/// Secured self-service account settings for Distributor (AD) / Master Distributor (MD)
/// partners: validate identity, change password (BCrypt), MPIN, and Txn PIN.
/// The acting user id always comes from the JWT, never from the request body.
/// </summary>
[ApiController]
[Authorize(Policy = "PartnerDashboard")]
[Route("api/v1/partner/account")]
[Produces("application/json")]
public sealed class PartnerAccountController : ControllerBase
{
    private readonly IPartnerAccountService _accountService;
    private readonly ILogger<PartnerAccountController> _logger;

    public PartnerAccountController(
        IPartnerAccountService accountService,
        ILogger<PartnerAccountController> logger)
    {
        _accountService = accountService;
        _logger = logger;
    }

    [HttpGet("profile")]
    public async Task<IActionResult> GetProfile(CancellationToken cancellationToken)
    {
        if (!TryGetPartnerIdentity(out var partnerId, out var userType))
        {
            return Unauthorized();
        }

        var profile = await _accountService.GetProfileAsync(partnerId, userType, cancellationToken);
        return profile == null ? NotFound() : Ok(profile);
    }

    [HttpPost("validate-and-send-otp")]
    public async Task<IActionResult> ValidateAndSendOtp(
        [FromBody] PartnerValidateAccountRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetPartnerIdentity(out var partnerId, out var userType))
        {
            return Unauthorized();
        }

        var result = await _accountService.ValidateAndSendOtpAsync(
            partnerId,
            userType,
            request,
            cancellationToken);

        return Ok(result);
    }

    [HttpPost("resend-otp")]
    public async Task<IActionResult> ResendOtp(CancellationToken cancellationToken)
    {
        if (!TryGetPartnerIdentity(out var partnerId, out _))
        {
            return Unauthorized();
        }

        var result = await _accountService.ResendOtpAsync(partnerId, cancellationToken);
        return Ok(result);
    }

    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword(
        [FromBody] PartnerChangePasswordRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetPartnerIdentity(out var partnerId, out _))
        {
            return Unauthorized();
        }

        var result = await _accountService.ChangePasswordAsync(partnerId, request, cancellationToken);
        return Ok(result);
    }

    [HttpPost("change-mpin")]
    public async Task<IActionResult> ChangeMpin(
        [FromBody] PartnerChangeMpinRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetPartnerIdentity(out var partnerId, out _))
        {
            return Unauthorized();
        }

        var result = await _accountService.ChangeMpinAsync(partnerId, request, cancellationToken);
        return Ok(result);
    }

    [HttpPost("change-txn-pin")]
    public async Task<IActionResult> ChangeTxnPin(
        [FromBody] PartnerChangeTxnPinRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetPartnerIdentity(out var partnerId, out _))
        {
            return Unauthorized();
        }

        var result = await _accountService.ChangeTxnPinAsync(partnerId, request, cancellationToken);
        return Ok(result);
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
}

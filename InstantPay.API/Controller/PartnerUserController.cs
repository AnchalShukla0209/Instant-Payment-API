using System.Security.Claims;
using InstantPay.Application.DTOs;
using InstantPay.Application.Interfaces;
using InstantPay.Infrastructure.Sql.Entities;
using InstantPay.SharedKernel.Entity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using InstantPay.SharedKernel.Enums;

namespace InstantPay.API.Controller;

/// <summary>
/// Secured endpoints for Distributor (AD) / Master Distributor (MD) partners to manage
/// the retailer/client users mapped under their own network. Only JWT-authenticated
/// AD/MD accounts (see the "PartnerDashboard" policy) can reach these endpoints; the
/// scope (Adid/Mdid) is always derived from the caller's own JWT claims, never from
/// client-supplied values, so a partner cannot read or modify another partner's data.
/// </summary>
[ApiController]
[Authorize(Policy = "PartnerDashboard")]
[Route("api/v1/partner/users")]
[Produces("application/json")]
public sealed class PartnerUserController : ControllerBase
{
    private readonly IClientUserOperation _reportService;
    private readonly IClientUserVerificationService _verificationService;
    private readonly IPlanDetailService _planDetailService;
    private readonly AppDbContext _dbContext;
    private readonly ILogger<PartnerUserController> _logger;

    public PartnerUserController(
        IClientUserOperation reportService,
        IClientUserVerificationService verificationService,
        IPlanDetailService planDetailService,
        AppDbContext dbContext,
        ILogger<PartnerUserController> logger)
    {
        _reportService = reportService;
        _verificationService = verificationService;
        _planDetailService = planDetailService;
        _dbContext = dbContext;
        _logger = logger;
    }

    [HttpGet("commission-plans")]
    public async Task<IActionResult> GetCommissionPlans()
    {
        try
        {
            var result = await _planDetailService.GetPlanDetailsForDropdown();
            return Ok(new { success = true, data = result });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load commission plans for partner dropdown.");
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    [HttpPost("identity-availability")]
    public async Task<IActionResult> IdentityAvailability(
        [FromBody] IdentityAvailabilityRequest request,
        CancellationToken cancellationToken)
    {
        var username = request.Username?.Trim();
        var phone = request.Phone?.Trim();
        var email = request.EmailId?.Trim().ToLowerInvariant();
        var pan = request.PanCard?.Trim().ToUpperInvariant();
        var aadhaar = request.AadharCard?.Trim();
        var conflicts = _dbContext.TblUsers.AsNoTracking().Where(x => x.Id != request.UserId &&
            (x.Status == "Active" || x.OnboardingStatus == OnboardingStatuses.PendingReview ||
             x.OnboardingStatus == OnboardingStatuses.PendingReReview));

        var result = new IdentityAvailabilityResponse(
            string.IsNullOrWhiteSpace(username) || !await conflicts.AnyAsync(x => x.Username == username, cancellationToken),
            string.IsNullOrWhiteSpace(phone) || !await conflicts.AnyAsync(x => x.Phone == phone, cancellationToken),
            string.IsNullOrWhiteSpace(email) || !await conflicts.AnyAsync(x => x.EmailId == email, cancellationToken),
            string.IsNullOrWhiteSpace(pan) || !await conflicts.AnyAsync(x => x.PanCard == pan, cancellationToken),
            string.IsNullOrWhiteSpace(aadhaar) || !await conflicts.AnyAsync(x => x.AadharCard == aadhaar, cancellationToken));
        return Ok(new { success = true, data = result });
    }

    [HttpPost("report")]
    public async Task<IActionResult> Report([FromBody] GetClientUserQuery request)
    {
        if (!TryGetPartnerIdentity(out var partnerId, out var userType))
        {
            return Unauthorized();
        }

        request ??= new GetClientUserQuery();
        request.ClientId = partnerId;
        request.ScopeType = userType;

        var result = await _reportService.GetClientUserList(request);
        return Ok(result);
    }

    [HttpPost("CreateOrUpdateClient")]
    public async Task<IActionResult> CreateOrUpdateClient(
        [FromForm] CreateOrUpdateClientUserCommand request,
        CancellationToken cancellationToken)
    {
        if (!TryGetPartnerIdentity(out var partnerId, out var userType))
        {
            return Unauthorized();
        }

        // Distributors/Master Distributors may only ONBOARD new users under their own
        // network - they are never allowed to edit an existing user's details (that stays
        // an admin-only capability). ClientId > 0 on this DTO means "editing user #ClientId".
        if (request.ClientId > 0)
        {
            return Forbid();
        }

        var partnerAccount = await _dbContext.TblUsers
            .AsNoTracking()
            .FirstOrDefaultAsync(
                u => u.Id == partnerId &&
                     u.Usertype == userType &&
                     u.Status == "Active",
                cancellationToken);

        if (partnerAccount == null)
        {
            return Unauthorized();
        }

        if (string.IsNullOrWhiteSpace(partnerAccount.Wlid) || partnerAccount.Wlid == "0")
        {
            return BadRequest(new
            {
                success = false,
                message = "White-label mapping is missing for this partner account."
            });
        }

        request.ScopeType = userType;
        request.ScopePartnerId = partnerId;
        request.WLID = partnerAccount.Wlid.Trim();

        var result = await _reportService.CreateOrUpdateClientUser(request, cancellationToken);
        return Ok(result);
    }

    [HttpGet("clientId")]
    public async Task<IActionResult> GetClientDetail(int Id)
    {
        if (!TryGetPartnerIdentity(out var partnerId, out var userType))
        {
            return Unauthorized();
        }

        if (!await _reportService.IsUserInScopeAsync(Id, userType, partnerId.ToString()))
        {
            return Forbid();
        }

        var client = await _reportService.GetClientUserDetailByIdAsync(Id);
        if (client == null)
            return NotFound("Client not found");

        return Ok(client);
    }

    [HttpDelete("delete-file")]
    public async Task<IActionResult> DeleteClientFile(int clientId, string fileType, CancellationToken cancellationToken)
    {
        if (!TryGetPartnerIdentity(out var partnerId, out var userType))
        {
            return Unauthorized();
        }

        if (!await _reportService.IsUserInScopeAsync(clientId, userType, partnerId.ToString()))
        {
            return Forbid();
        }

        var command = new DeleteClientUserFileCommand
        {
            ClientId = clientId,
            FileType = fileType
        };

        var result = await _reportService.HandleDeleteClientUserFile(command, cancellationToken);
        return Ok(result);
    }

    [HttpPost("wallet-transaction")]
    public async Task<IActionResult> WalletTransaction([FromBody] WalletTransactionRequest request)
    {
        if (!TryGetPartnerIdentity(out var partnerId, out var userType))
        {
            return Unauthorized();
        }

        if (!await _reportService.IsUserInScopeAsync(request.UserId, userType, partnerId.ToString()))
        {
            return Forbid();
        }

        request.ActionById = partnerId;

        // Always a real transfer between the partner's own wallet and their downline's wallet
        // (never a one-sided top-up) — see TransferWalletForPartnerAsync for the atomicity guarantee.
        var response = await _reportService.TransferWalletForPartnerAsync(request);
        return Ok(response);
    }

    [HttpPost("send-phone-otp")]
    public async Task<IActionResult> SendPhoneOtp([FromBody] SendClientUserOtpRequest request)
    {
        var result = await _verificationService.SendPhoneOtpAsync(request.Value);
        return Ok(result);
    }

    [HttpPost("send-email-otp")]
    public async Task<IActionResult> SendEmailOtp([FromBody] SendClientUserOtpRequest request)
    {
        var result = await _verificationService.SendEmailOtpAsync(request.Value);
        return Ok(result);
    }

    [HttpPost("verify-otp")]
    public async Task<IActionResult> VerifyOtp([FromBody] VerifyClientUserOtpRequest request)
    {
        if (!await IsVerificationTargetAllowedAsync(request.ClientId))
        {
            return Forbid();
        }

        var result = await _verificationService.VerifyOtpAsync(request);
        return Ok(result);
    }

    [HttpPost("verify-pan")]
    public async Task<IActionResult> VerifyPan([FromBody] VerifyClientUserPanRequest request)
    {
        if (!await IsVerificationTargetAllowedAsync(request.ClientId))
        {
            return Forbid();
        }

        var result = await _verificationService.VerifyPanAsync(request.PanNumber, request.ClientId);
        return Ok(result);
    }

    [HttpPost("verify-aadhaar")]
    public async Task<IActionResult> VerifyAadhaar([FromBody] VerifyClientUserAadhaarRequest request)
    {
        if (!await IsVerificationTargetAllowedAsync(request.ClientId))
        {
            return Forbid();
        }

        var result = await _verificationService.VerifyAadhaarAsync(request.AadharNumber, request.ClientId);
        return Ok(result);
    }

    private async Task<bool> IsVerificationTargetAllowedAsync(int clientId)
    {
        if (clientId <= 0)
        {
            // New user being created — nothing to scope-check yet.
            return true;
        }

        if (!TryGetPartnerIdentity(out var partnerId, out var userType))
        {
            return false;
        }

        return await _reportService.IsUserInScopeAsync(clientId, userType, partnerId.ToString());
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

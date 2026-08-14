using System.Security.Claims;
using InstantPay.Application.Interfaces;
using InstantPay.SharedKernel.Entity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InstantPay.API.Controller;

/// <summary>
/// Secured self-service wallet top-up endpoints for Distributor (AD) / Master Distributor (MD)
/// partners - the same "raise a payment request against a company bank account" flow retailers
/// get on `/Payment-Request`, plus their own request history (`/Payment-Request-User-Report`).
/// Only JWT-authenticated AD/MD accounts (see the "PartnerDashboard" policy) can reach these
/// endpoints, and the acting user id is always derived from the caller's own JWT claims, never
/// from a client-supplied value - a partner can only raise/view requests for themselves.
/// </summary>
[ApiController]
[Authorize(Policy = "PartnerDashboard")]
[Route("api/v1/partner/payment")]
[Produces("application/json")]
public sealed class PartnerPaymentController : ControllerBase
{
    private readonly IPaymentService _paymentService;
    private readonly IBankRepository _bankService;
    private readonly ILogger<PartnerPaymentController> _logger;

    public PartnerPaymentController(
        IPaymentService paymentService,
        IBankRepository bankService,
        ILogger<PartnerPaymentController> logger)
    {
        _paymentService = paymentService;
        _bankService = bankService;
        _logger = logger;
    }

    [HttpGet("banks/active")]
    public async Task<IActionResult> GetActiveBanks()
    {
        if (!TryGetPartnerIdentity(out _, out _))
        {
            return Unauthorized();
        }

        var banks = await _bankService.GetAllActiveAsync();
        return Ok(banks);
    }

    [HttpGet("banks/{bankId:guid}")]
    public async Task<IActionResult> GetBankById(Guid bankId)
    {
        if (!TryGetPartnerIdentity(out _, out _))
        {
            return Unauthorized();
        }

        var bank = await _bankService.GetByIdAsync(bankId);
        return bank == null ? NotFound() : Ok(bank);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromForm] PaymentRequestDto request)
    {
        if (!TryGetPartnerIdentity(out var partnerId, out _))
        {
            return Unauthorized();
        }

        try
        {
            // The DTO's own UserId (if any) is ignored - the request is always raised for the
            // authenticated partner themselves.
            var id = await _paymentService.CreatePaymentRequestAsync(request, partnerId);
            return Ok(id);
        }
        catch (ArgumentException ae)
        {
            return BadRequest(new { Success = false, Message = ae.Message });
        }
        catch (IOException ex)
        {
            return BadRequest(new { Success = false, Message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create partner payment request.");
            return BadRequest(new { Success = false, Message = ex.Message });
        }
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string status = null,
        [FromQuery] string? fromDate = null,
        [FromQuery] string? toDate = null,
        [FromQuery] string commonsearch = "",
        [FromQuery] int isExport = 0)
    {
        if (!TryGetPartnerIdentity(out var partnerId, out _))
        {
            return Unauthorized();
        }

        try
        {
            // Always scoped to the caller's own requests - never a client-supplied userid.
            var result = await _paymentService.GetAllPaymentsAsync(
                pageNumber, pageSize, status, fromDate, toDate, commonsearch, isExport, partnerId);
            return Ok(new { result.Payments, result.TotalCount });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load partner payment report.");
            return BadRequest(new { Success = false, Message = ex.Message });
        }
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        if (!TryGetPartnerIdentity(out var partnerId, out _))
        {
            return Unauthorized();
        }

        var result = await _paymentService.GetPaymentByIdAsync(id);
        if (result == null || !await IsOwnPaymentAsync(id, partnerId))
        {
            return NotFound();
        }

        return Ok(result);
    }

    [HttpGet("download/{id:guid}")]
    public async Task<IActionResult> Download(Guid id)
    {
        if (!TryGetPartnerIdentity(out var partnerId, out _))
        {
            return Unauthorized();
        }

        if (!await IsOwnPaymentAsync(id, partnerId))
        {
            return Forbid();
        }

        try
        {
            var file = await _paymentService.DownloadTxnSlipAsync(id);
            return File(file.FileContent, file.ContentType, file.FileName);
        }
        catch (FileNotFoundException ex)
        {
            return BadRequest(new { Success = false, Message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to download partner payment slip.");
            return BadRequest(new { Success = false, Message = ex.Message });
        }
    }

    private async Task<bool> IsOwnPaymentAsync(Guid paymentId, int partnerId)
    {
        // GetPaymentByIdAsync doesn't take a userid filter, so ownership is verified by
        // cross-checking against this partner's own (unpaginated - isExport bypasses
        // paging) request list instead.
        var owned = await _paymentService.GetAllPaymentsAsync(1, 1, null, null, null, string.Empty, 1, partnerId);
        return owned.Payments.Any(p => p.PaymentId == paymentId);
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

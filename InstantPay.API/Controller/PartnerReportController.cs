using System.Security.Claims;
using System.Text.Json;
using InstantPay.Application.Interfaces;
using InstantPay.SharedKernel.Entity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InstantPay.API.Controller;

/// <summary>
/// Downline transaction report for Distributor (AD) / Master Distributor (MD) partners -
/// mirrors the retailer `/UserTxnReports` page (same column set, filters, and service-type
/// branches), but instead of showing a single retailer's own transactions it shows every
/// transaction belonging to users under the partner's network (TblUsers.Adid/Mdid ==
/// partnerId) plus the partner's own. Scope is always derived from the caller's own JWT
/// claims (see "PartnerDashboard" policy) - never from a client-supplied user id.
/// </summary>
[ApiController]
[Authorize(Policy = "PartnerDashboard")]
[Route("api/v1/partner/report")]
[Produces("application/json")]
public sealed class PartnerReportController : ControllerBase
{
    private readonly IReportService _reportService;
    private readonly ILogger<PartnerReportController> _logger;

    public PartnerReportController(IReportService reportService, ILogger<PartnerReportController> logger)
    {
        _reportService = reportService;
        _logger = logger;
    }

    [HttpPost("txn-report")]
    public async Task<IActionResult> GetTxnReport([FromBody] TxnReportPayload request)
    {
        if (!TryGetPartnerIdentity(out var partnerId, out var userType))
        {
            return Unauthorized();
        }

        try
        {
            var result = await _reportService.GetPartnerTransactionReportAsync(
                partnerId,
                userType,
                request.serviceType,
                request.status,
                request.dateFrom,
                request.dateTo,
                request.pageIndex ?? 1,
                request.pageSize ?? 10,
                request.commonsearch ?? string.Empty,
                request.ispaginationenabled ?? 1,
                request.userId ?? 0);

            // Serialized manually (System.Text.Json, default PascalCase) rather than via Ok(),
            // to exactly match the field names (TXN_ID, BankRefNo, TotalTransactions, ...) the
            // existing retailer report already expects - the app's MVC pipeline is otherwise
            // configured for Newtonsoft's camelCase, which would silently rename these.
            var json = JsonSerializer.Serialize(result);
            return Content(json, "application/json");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load partner transaction report.");
            return BadRequest(new { Success = false, Message = ex.Message });
        }
    }

    [HttpGet("users/dropdown")]
    public async Task<IActionResult> GetUsersDropdown()
    {
        if (!TryGetPartnerIdentity(out var partnerId, out var userType))
        {
            return Unauthorized();
        }

        var users = await _reportService.GetPartnerUserDropdownAsync(partnerId, userType);
        return Ok(users);
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

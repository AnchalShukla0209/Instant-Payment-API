using InstantPay.Application.Interfaces.RBL;
using InstantPay.SharedKernel.RequestPayload.RBL;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InstantPay.API.Controller;

[ApiController]
[Route("api/rbl/statement")]
[Authorize(Policy = "SuperAdminOnly")]
public sealed class RblStatementController : ControllerBase
{
    private readonly IRblStatementService _statementService;

    public RblStatementController(IRblStatementService statementService)
    {
        _statementService = statementService;
    }

    [HttpPost("date-range")]
    [Produces("application/json")]
    public async Task<IActionResult> GetDateRange(
        [FromBody] RblDateRangeStatementRequest request, CancellationToken cancellationToken)
    {
        var result = await _statementService.GetDateRangeAsync(request, cancellationToken);
        return ToActionResult(result);
    }

    [HttpPost("period")]
    [Produces("application/json")]
    public async Task<IActionResult> GetPeriod(
        [FromBody] RblPeriodStatementRequest request, CancellationToken cancellationToken)
    {
        var result = await _statementService.GetPeriodAsync(request, cancellationToken);
        return ToActionResult(result);
    }

    private IActionResult ToActionResult(RblStatementApiResult result)
    {
        if (result.Success)
            return Content(result.ResponseJson, "application/json");

        if (!string.IsNullOrWhiteSpace(result.ResponseJson))
            return new ContentResult { StatusCode = StatusCodes.Status502BadGateway, ContentType = "application/json", Content = result.ResponseJson };

        return StatusCode(result.ErrorStatusCode, new { message = result.ErrorMessage });
    }
}

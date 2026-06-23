using InstantPay.Application.DTOs;
using InstantPay.Application.Interfaces.PPI;
using Microsoft.AspNetCore.Mvc;

namespace InstantPay.API.Controller.PPI;

[ApiController]
[Route("api/PPI/[controller]")]
public class PPIMoneyTransferController : ControllerBase
{
    private readonly IPPIMoneyTransferService _service;
    private readonly ILogger<PPIMoneyTransferController> _logger;

    public PPIMoneyTransferController(IPPIMoneyTransferService service, ILogger<PPIMoneyTransferController> logger)
    {
        _service = service;
        _logger = logger;
    }

    [HttpPost("Transfer")]
    public async Task<IActionResult> Transfer([FromBody] PPIMoneyTransferRequest request)
    {
        if (request == null || !ModelState.IsValid)
        {
            var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
            return BadRequest(new PPIMoneyTransferResponse
            {
                Status_Code = "0",
                Message = request == null ? "Invalid request payload" : string.Join(", ", errors),
                Data = ""
            });
        }

        try
        {
            _logger.LogInformation("PPIMoneyTransfer | UserId: {UserId} Mobile: {Mobile} AccountNo: {AccountNo} Amount: {Amount}",
                request.UserId, request.Sendermobile, request.AccountNo, request.Amount);

            var result = await _service.MoneyTransferAsync(request);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in PPIMoneyTransfer endpoint");
            return StatusCode(500, new PPIMoneyTransferResponse
            {
                Status_Code = "0",
                Message = "Internal server error",
                Data = ""
            });
        }
    }
}

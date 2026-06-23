using InstantPay.Application.DTOs;
using InstantPay.Application.Interfaces.PPI;
using Microsoft.AspNetCore.Mvc;

namespace InstantPay.API.Controller.PPI;

[ApiController]
[Route("api/PPI/[controller]")]
public class PPIFundTransferController : ControllerBase
{
    private readonly IPPIFundTransferService _service;
    private readonly ILogger<PPIFundTransferController> _logger;

    public PPIFundTransferController(IPPIFundTransferService service, ILogger<PPIFundTransferController> logger)
    {
        _service = service;
        _logger = logger;
    }

    [HttpPost("GetOtp")]
    public async Task<IActionResult> GetOtp([FromBody] PPIFundTransferOtpRequest request)
    {
        if (request == null || !ModelState.IsValid)
        {
            var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
            return BadRequest(new PPIFundTransferOtpResponse
            {
                Status_Code = "0",
                Message = request == null ? "Invalid request payload" : string.Join(", ", errors),
                Data = ""
            });
        }

        try
        {
            _logger.LogInformation("FundTransfer GetOtp | Mobile: {Mobile} BeneId: {BeneId} Amount: {Amount}",
                request.MobileNumber, request.BeneficiaryId, request.Amount);

            var result = await _service.GetOtpAsync(request);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in FundTransfer GetOtp endpoint");
            return StatusCode(500, new PPIFundTransferOtpResponse
            {
                Status_Code = "0",
                Message = "Internal server error",
                Data = ""
            });
        }
    }
}

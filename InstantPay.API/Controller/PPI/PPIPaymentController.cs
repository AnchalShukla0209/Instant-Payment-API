using InstantPay.Application.DTOs;
using InstantPay.Application.Interfaces.PPI;
using Microsoft.AspNetCore.Mvc;

namespace InstantPay.API.Controller.PPI;

[ApiController]
[Route("api/PPI/[controller]")]
public class PPIPaymentController : ControllerBase
{
    private readonly IPPIPaymentService _ppiPaymentService;
    private readonly ILogger<PPIPaymentController> _logger;

    public PPIPaymentController(IPPIPaymentService ppiPaymentService, ILogger<PPIPaymentController> logger)
    {
        _ppiPaymentService = ppiPaymentService;
        _logger = logger;
    }

    [HttpPost("SendPaymentOtp")]
    public async Task<IActionResult> SendPaymentOtp([FromBody] PPISendPaymentOtpRequest request)
    {
        try
        {
            if (request == null)
            {
                _logger.LogWarning("SendPaymentOtp called with null request");
                return BadRequest(new PPISendPaymentOtpResponse
                {
                    Status_Code = "0",
                    Message = "Invalid request payload",
                    Data = ""
                });
            }

            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
                _logger.LogWarning("SendPaymentOtp validation failed: {Errors}", string.Join(", ", errors));
                return BadRequest(new PPISendPaymentOtpResponse
                {
                    Status_Code = "0",
                    Message = "Validation failed: " + string.Join(", ", errors),
                    Data = ""
                });
            }

            _logger.LogInformation("SendPaymentOtp called for Mobile: {Mobile}, BeneId: {BeneId}, Amount: {Amount}", 
                request.SenderMobile, request.BeneId, request.Amount);

            var result = await _ppiPaymentService.SendPaymentOtpAsync(request);

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in SendPaymentOtp endpoint");
            return StatusCode(500, new PPISendPaymentOtpResponse
            {
                Status_Code = "0",
                Message = "Internal server error",
                Data = ""
            });
        }
    }
}

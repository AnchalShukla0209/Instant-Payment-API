using InstantPay.Application.DTOs;
using InstantPay.Application.Interfaces.PPI;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InstantPay.API.Controller.PPI;


[ApiController]
[Route("api/PPI/[controller]")]
public class PPIOtpController : ControllerBase
{
    private readonly IPPIOtpService _ppiOtpService;
    private readonly ILogger<PPIOtpController> _logger;

    public PPIOtpController(IPPIOtpService ppiOtpService, ILogger<PPIOtpController> logger)
    {
        _ppiOtpService = ppiOtpService;
        _logger = logger;
    }

    [HttpPost("GenerateOtp")]
    public async Task<IActionResult> GeneratePPIOtp([FromBody] GeneratePPIOtpRequest request)
    {
        try
        {
            if (request == null)
            {
                _logger.LogWarning("GeneratePPIOtp called with null request");
                return BadRequest(new GeneratePPIOtpResponse
                {
                    Status_Code = "0",
                    Message = "Invalid request payload",
                    Data = ""
                });
            }

            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
                _logger.LogWarning("GeneratePPIOtp validation failed: {Errors}", string.Join(", ", errors));
                return BadRequest(new GeneratePPIOtpResponse
                {
                    Status_Code = "0",
                    Message = "Validation failed: " + string.Join(", ", errors),
                    Data = ""
                });
            }

            _logger.LogInformation("GeneratePPIOtp called for UserId: {UserId}, Mobile: {Mobile}", 
                request.UserId, request.SenderMobile);

            var result = await _ppiOtpService.GenerateOtpAsync(request);

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GeneratePPIOtp endpoint");
            return StatusCode(500, new GeneratePPIOtpResponse
            {
                Status_Code = "0",
                Message = "Internal server error",
                Data = ""
            });
        }
    }

    [HttpPost("VerifyOtp")]
    public async Task<IActionResult> VerifyPPIOtp([FromBody] VerifyPPIOtpRequest request)
    {
        try
        {
            if (request == null)
            {
                _logger.LogWarning("VerifyPPIOtp called with null request");
                return BadRequest(new VerifyPPIOtpResponse
                {
                    Status_Code = "0",
                    Message = "Invalid request payload",
                    Data = new List<PPIWalletDetail>()
                });
            }

            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
                _logger.LogWarning("VerifyPPIOtp validation failed: {Errors}", string.Join(", ", errors));
                return BadRequest(new VerifyPPIOtpResponse
                {
                    Status_Code = "0",
                    Message = "Validation failed: " + string.Join(", ", errors),
                    Data = new List<PPIWalletDetail>()
                });
            }

            _logger.LogInformation("VerifyPPIOtp called for UserId: {UserId}", request.UserId);

            var result = await _ppiOtpService.VerifyOtpAsync(request);

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in VerifyPPIOtp endpoint");
            return StatusCode(500, new VerifyPPIOtpResponse
            {
                Status_Code = "0",
                Message = "Internal server error",
                Data = new List<PPIWalletDetail>()
            });
        }
    }
}

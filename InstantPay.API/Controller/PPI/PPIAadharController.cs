using InstantPay.Application.DTOs;
using InstantPay.Application.Interfaces.PPI;
using Microsoft.AspNetCore.Mvc;

namespace InstantPay.API.Controller.PPI;

[ApiController]
[Route("api/PPI/[controller]")]
public class PPIAadharController : ControllerBase
{
    private readonly IPPIAadharService _ppiAadharService;
    private readonly ILogger<PPIAadharController> _logger;

    public PPIAadharController(IPPIAadharService ppiAadharService, ILogger<PPIAadharController> logger)
    {
        _ppiAadharService = ppiAadharService;
        _logger = logger;
    }

    [HttpPost("GenerateAadharOtp")]
    public async Task<IActionResult> GenerateAadharOtp([FromBody] PPIAadharOtpRequest request)
    {
        try
        {
            if (request == null)
            {
                _logger.LogWarning("GenerateAadharOtp called with null request");
                return BadRequest(new PPIAadharOtpResponse
                {
                    Status_Code = "0",
                    Message = "Invalid request payload",
                    Data = ""
                });
            }

            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
                _logger.LogWarning("GenerateAadharOtp validation failed: {Errors}", string.Join(", ", errors));
                return BadRequest(new PPIAadharOtpResponse
                {
                    Status_Code = "0",
                    Message = "Validation failed: " + string.Join(", ", errors),
                    Data = ""
                });
            }

            _logger.LogInformation("GenerateAadharOtp called for AadharNo: {AadharNo}", request.AadharNo);

            var result = await _ppiAadharService.GenerateAadharOtpAsync(request);

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GenerateAadharOtp endpoint");
            return StatusCode(500, new PPIAadharOtpResponse
            {
                Status_Code = "0",
                Message = "Internal server error",
                Data = ""
            });
        }
    }

    [HttpPost("ValidateAadharOtp")]
    public async Task<IActionResult> ValidateAadharOtp([FromBody] PPIValidateAadharOtpRequest request)
    {
        try
        {
            if (request == null)
            {
                _logger.LogWarning("ValidateAadharOtp called with null request");
                return BadRequest(new PPIValidateAadharOtpResponse
                {
                    Status_Code = "0",
                    Message = "Invalid request payload",
                    Data = ""
                });
            }

            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
                _logger.LogWarning("ValidateAadharOtp validation failed: {Errors}", string.Join(", ", errors));
                return BadRequest(new PPIValidateAadharOtpResponse
                {
                    Status_Code = "0",
                    Message = "Validation failed: " + string.Join(", ", errors),
                    Data = ""
                });
            }

            _logger.LogInformation("ValidateAadharOtp called for ApplicationNumber: {ApplicationNumber}, SenderMobile: {SenderMobile}", 
                request.ApplicationNumber, request.SenderMobile);

            var result = await _ppiAadharService.ValidateAadharOtpAsync(request);

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in ValidateAadharOtp endpoint");
            return StatusCode(500, new PPIValidateAadharOtpResponse
            {
                Status_Code = "0",
                Message = "Internal server error",
                Data = ""
            });
        }
    }

    [HttpPost("AadharBiometric")]
    public async Task<IActionResult> AadharBiometric([FromBody] PPIAadharBiometricRequest request)
    {
        try
        {
            if (request == null)
            {
                _logger.LogWarning("AadharBiometric called with null request");
                return BadRequest(new PPIAadharBiometricResponse
                {
                    Status_Code = "0",
                    Message = "Invalid request payload",
                    Data = ""
                });
            }

            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
                _logger.LogWarning("AadharBiometric validation failed: {Errors}", string.Join(", ", errors));
                return BadRequest(new PPIAadharBiometricResponse
                {
                    Status_Code = "0",
                    Message = "Validation failed: " + string.Join(", ", errors),
                    Data = ""
                });
            }

            _logger.LogInformation("AadharBiometric called for ApplicationNumber: {ApplicationNumber}, SenderMobile: {SenderMobile}", 
                request.ApplicationNumber, request.SenderMobile);

            var result = await _ppiAadharService.AadharBiometricAsync(request);

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in AadharBiometric endpoint");
            return StatusCode(500, new PPIAadharBiometricResponse
            {
                Status_Code = "0",
                Message = "Internal server error",
                Data = ""
            });
        }
    }

    [HttpPost("ValidatePan")]
    public async Task<IActionResult> ValidatePan([FromBody] PPIPanRequest request)
    {
        try
        {
            if (request == null)
            {
                _logger.LogWarning("ValidatePan called with null request");
                return BadRequest(new PPIPanResponse
                {
                    Status_Code = "0",
                    Message = "Invalid request payload",
                    Data = ""
                });
            }

            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
                _logger.LogWarning("ValidatePan validation failed: {Errors}", string.Join(", ", errors));
                return BadRequest(new PPIPanResponse
                {
                    Status_Code = "0",
                    Message = "Validation failed: " + string.Join(", ", errors),
                    Data = ""
                });
            }

            _logger.LogInformation("ValidatePan called for ApplicationNumber: {ApplicationNumber}, PancardNo: {PancardNo}", 
                request.ApplicationNumber, request.PancardNo);

            var result = await _ppiAadharService.ValidatePanAsync(request);

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in ValidatePan endpoint");
            return StatusCode(500, new PPIPanResponse
            {
                Status_Code = "0",
                Message = "Internal server error",
                Data = ""
            });
        }
    }
}

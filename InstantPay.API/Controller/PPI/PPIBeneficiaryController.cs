using InstantPay.Application.DTOs;
using InstantPay.Application.Interfaces.PPI;
using Microsoft.AspNetCore.Mvc;

namespace InstantPay.API.Controller.PPI;

[ApiController]
[Route("api/PPI/[controller]")]
public class PPIBeneficiaryController : ControllerBase
{
    private readonly IPPIBeneficiaryService _ppiBeneficiaryService;
    private readonly ILogger<PPIBeneficiaryController> _logger;

    public PPIBeneficiaryController(IPPIBeneficiaryService ppiBeneficiaryService, ILogger<PPIBeneficiaryController> logger)
    {
        _ppiBeneficiaryService = ppiBeneficiaryService;
        _logger = logger;
    }

    [HttpPost("GetBeneficiaryList")]
    public async Task<IActionResult> GetBeneficiaryList([FromBody] PPIBeneListRequest request)
    {
        try
        {
            if (request == null)
            {
                _logger.LogWarning("GetBeneficiaryList called with null request");
                return BadRequest(new PPIBeneListResponse
                {
                    Status_Code = "0",
                    Message = "Invalid request payload",
                    Data = new List<PPIBeneficiary>()
                });
            }

            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
                _logger.LogWarning("GetBeneficiaryList validation failed: {Errors}", string.Join(", ", errors));
                return BadRequest(new PPIBeneListResponse
                {
                    Status_Code = "0",
                    Message = "Validation failed: " + string.Join(", ", errors),
                    Data = new List<PPIBeneficiary>()
                });
            }

            _logger.LogInformation("GetBeneficiaryList called for Mobile: {Mobile}", request.SenderMobile);

            var result = await _ppiBeneficiaryService.GetBeneficiaryListAsync(request);

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetBeneficiaryList endpoint");
            return StatusCode(500, new PPIBeneListResponse
            {
                Status_Code = "0",
                Message = "Internal server error",
                Data = new List<PPIBeneficiary>()
            });
        }
    }

    [HttpPost("AddBeneficiary")]
    public async Task<IActionResult> AddBeneficiary([FromBody] PPIAddBeneRequest request)
    {
        try
        {
            if (request == null)
            {
                _logger.LogWarning("AddBeneficiary called with null request");
                return BadRequest(new PPIAddBeneResponse
                {
                    Status_Code = "0",
                    Message = "Invalid request payload",
                    Data = ""
                });
            }

            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
                _logger.LogWarning("AddBeneficiary validation failed: {Errors}", string.Join(", ", errors));
                return BadRequest(new PPIAddBeneResponse
                {
                    Status_Code = "0",
                    Message = "Validation failed: " + string.Join(", ", errors),
                    Data = ""
                });
            }

            _logger.LogInformation("AddBeneficiary called for Mobile: {Mobile}, Account: {Account}", 
                request.SenderMobile, request.AccountNo);

            var result = await _ppiBeneficiaryService.AddBeneficiaryAsync(request);

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in AddBeneficiary endpoint");
            return StatusCode(500, new PPIAddBeneResponse
            {
                Status_Code = "0",
                Message = "Internal server error",
                Data = ""
            });
        }
    }

    [HttpPost("ResendOtp")]
    public async Task<IActionResult> ResendOtp([FromBody] PPIResendOtpRequest request)
    {
        try
        {
            if (request == null)
            {
                _logger.LogWarning("ResendOtp called with null request");
                return BadRequest(new PPIResendOtpResponse
                {
                    Status_Code = "0",
                    Message = "Invalid request payload",
                    Data = ""
                });
            }

            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
                _logger.LogWarning("ResendOtp validation failed: {Errors}", string.Join(", ", errors));
                return BadRequest(new PPIResendOtpResponse
                {
                    Status_Code = "0",
                    Message = "Validation failed: " + string.Join(", ", errors),
                    Data = ""
                });
            }

            _logger.LogInformation("ResendOtp called for UserId: {UserId}", request.UserId);

            var result = await _ppiBeneficiaryService.ResendOtpAsync(request);

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in ResendOtp endpoint");
            return StatusCode(500, new PPIResendOtpResponse
            {
                Status_Code = "0",
                Message = "Internal server error",
                Data = ""
            });
        }
    }

    [HttpPost("ValidateOtp")]
    public async Task<IActionResult> ValidateOtp([FromBody] PPIValidateOtpRequest request)
    {
        try
        {
            if (request == null)
            {
                _logger.LogWarning("ValidateOtp called with null request");
                return BadRequest(new PPIValidateOtpResponse
                {
                    Status_Code = "0",
                    Message = "Invalid request payload",
                    Data = ""
                });
            }

            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
                _logger.LogWarning("ValidateOtp validation failed: {Errors}", string.Join(", ", errors));
                return BadRequest(new PPIValidateOtpResponse
                {
                    Status_Code = "0",
                    Message = "Validation failed: " + string.Join(", ", errors),
                    Data = ""
                });
            }

            _logger.LogInformation("ValidateOtp called for UserId: {UserId}", request.UserId);

            var result = await _ppiBeneficiaryService.ValidateOtpAsync(request);

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in ValidateOtp endpoint");
            return StatusCode(500, new PPIValidateOtpResponse
            {
                Status_Code = "0",
                Message = "Internal server error",
                Data = ""
            });
        }
    }

    [HttpPost("DeleteGetOtp")]
    public async Task<IActionResult> DeleteGetOtp([FromBody] PPIDeleteOtpRequest request)
    {
        try
        {
            if (request == null)
            {
                _logger.LogWarning("DeleteGetOtp called with null request");
                return BadRequest(new PPIDeleteOtpResponse
                {
                    Status_Code = "0",
                    Message = "Invalid request payload",
                    Data = ""
                });
            }

            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
                _logger.LogWarning("DeleteGetOtp validation failed: {Errors}", string.Join(", ", errors));
                return BadRequest(new PPIDeleteOtpResponse
                {
                    Status_Code = "0",
                    Message = "Validation failed: " + string.Join(", ", errors),
                    Data = ""
                });
            }

            _logger.LogInformation("DeleteGetOtp called for Mobile: {Mobile}, BeneficiaryId: {BeneficiaryId}", 
                request.mobilenumber, request.beneficiaryid);

            var result = await _ppiBeneficiaryService.DeleteGetOtpAsync(request);

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in DeleteGetOtp endpoint");
            return StatusCode(500, new PPIDeleteOtpResponse
            {
                Status_Code = "0",
                Message = "Internal server error",
                Data = ""
            });
        }
    }

    [HttpPost("DeleteVerifyOtp")]
    public async Task<IActionResult> DeleteVerifyOtp([FromBody] PPIDeleteVerifyRequest request)
    {
        try
        {
            if (request == null)
            {
                _logger.LogWarning("DeleteVerifyOtp called with null request");
                return BadRequest(new PPIDeleteVerifyResponse
                {
                    Status_Code = "0",
                    Message = "Invalid request payload",
                    Data = ""
                });
            }

            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
                _logger.LogWarning("DeleteVerifyOtp validation failed: {Errors}", string.Join(", ", errors));
                return BadRequest(new PPIDeleteVerifyResponse
                {
                    Status_Code = "0",
                    Message = "Validation failed: " + string.Join(", ", errors),
                    Data = ""
                });
            }

            _logger.LogInformation("DeleteVerifyOtp called for Mobile: {Mobile}", request.mobilenumber);

            var result = await _ppiBeneficiaryService.DeleteVerifyOtpAsync(request);

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in DeleteVerifyOtp endpoint");
            return StatusCode(500, new PPIDeleteVerifyResponse
            {
                Status_Code = "0",
                Message = "Internal server error",
                Data = ""
            });
        }
    }
}

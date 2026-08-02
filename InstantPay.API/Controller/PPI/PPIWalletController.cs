using InstantPay.Application.DTOs;
using InstantPay.Application.Interfaces.PPI;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InstantPay.API.Controller.PPI;


[ApiController]
[Route("api/PPI/[controller]")]
public class PPIWalletController : ControllerBase
{
    private readonly IPPIWalletService _ppiWalletService;
    private readonly ILogger<PPIWalletController> _logger;

    public PPIWalletController(IPPIWalletService ppiWalletService, ILogger<PPIWalletController> logger)
    {
        _ppiWalletService = ppiWalletService;
        _logger = logger;
    }

    [HttpPost("LoadWallet")]
    public async Task<IActionResult> LoadWallet([FromBody] PPILoadWalletRequest request)
    {
        try
        {
            if (request == null)
            {
                _logger.LogWarning("LoadWallet called with null request");
                return BadRequest(new PPILoadWalletResponse
                {
                    Status_Code = "0",
                    Message = "Invalid request payload",
                    Data = new()
                });
            }

            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
                _logger.LogWarning("LoadWallet validation failed: {Errors}", string.Join(", ", errors));
                return BadRequest(new PPILoadWalletResponse
                {
                    Status_Code = "0",
                    Message = "Validation failed: " + string.Join(", ", errors),
                    Data = new()
                });
            }

            _logger.LogInformation("LoadWallet called for Mobile: {Mobile}, Amount: {Amount}", 
                request.Sendermobile, request.Amount);

            var result = await _ppiWalletService.LoadWalletAsync(request);

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in LoadWallet endpoint");
            return StatusCode(500, new PPILoadWalletResponse
            {
                Status_Code = "0",
                Message = "Internal server error",
                Data = new()
            });
        }
    }
}

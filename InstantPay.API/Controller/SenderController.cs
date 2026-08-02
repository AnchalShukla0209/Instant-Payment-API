using InstantPay.Application.DTOs;
using InstantPay.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace InstantPay.API.Controller;

[ApiController]
[Route("api/[controller]")]
public class SenderController : ControllerBase
{
    private readonly ISenderService _senderService;

    public SenderController(ISenderService senderService)
    {
        _senderService = senderService;
    }

    [HttpPost("login")]
    public async Task<IActionResult> SenderLogin([FromBody] SenderLoginRequestDto request)
    {
        var response = await _senderService.SenderLoginAsync(request);
        return Ok(response);
    }

    [HttpPost("registration")]
    public async Task<IActionResult> SenderRegistration([FromBody] SenderRegistrationRequestDto request)
    {
        var response = await _senderService.SenderRegistrationAsync(request);
        return Ok(response);
    }

    [HttpPost("ekyc")]
    public async Task<IActionResult> SenderEkyc([FromBody] SenderEkycRequestDto request)
    {
        var response = await _senderService.SenderEkycAsync(request);
        return Ok(response);
    }
}

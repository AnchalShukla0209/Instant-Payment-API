using InstantPay.Application.DTOs;
using InstantPay.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InstantPay.API.Controller;

[ApiController]
[Route("api/[controller]")]
public class BeneficiaryController : ControllerBase
{
    private readonly IBeneficiaryService _beneficiaryService;

    public BeneficiaryController(IBeneficiaryService beneficiaryService)
    {
        _beneficiaryService = beneficiaryService;
    }

    [HttpPost("Save")]
    public async Task<IActionResult> SaveBeneficiary([FromBody] SaveBeneficiaryRequest request)
    {
        if (request == null) return BadRequest("Invalid request");

        var result = await _beneficiaryService.SaveBeneficiaryAsync(request);
        return Ok(result);
    }

    [HttpPost("SendOtp")]
    public async Task<IActionResult> SendOtp([FromBody] SendOtpRequest request)
    {
        if (request == null) return BadRequest("Invalid request");

        var result = await _beneficiaryService.SendOtpAsync(request);
        return Ok(result);
    }

    [HttpPost("ResendOtp")]
    public async Task<IActionResult> ResendOtp([FromBody] SendOtpRequest request)
    {
        if (request == null) return BadRequest("Invalid request");

        var result = await _beneficiaryService.ResendOtpAsync(request);
        return Ok(result);
    }

    [HttpPost("Delete")]
    public async Task<IActionResult> DeleteBeneficiary([FromBody] DeleteBeneficiaryRequest request)
    {
        if (request == null) return BadRequest("Invalid request");

        var result = await _beneficiaryService.DeleteBeneficiaryAsync(request);
        return Ok(result);
    }

    [HttpPost("GetBeneficiaryList")]
    public async Task<IActionResult> GetBeneficiaryList([FromBody] GetBeneficiaryListRequest request)
    {
        if (request == null) return BadRequest("Invalid request");

        var result = await _beneficiaryService.GetBeneficiaryListAsync(request);
        return Ok(result);
    }
}

using InstantPay.Application.DTOs;
using InstantPay.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;

namespace InstantPay.API.Controller;

[ApiController]
[AllowAnonymous]
[Route("api/[controller]")]
public sealed class WebsiteEnquiryController : ControllerBase
{
    private static readonly TimeSpan SubmissionCooldown = TimeSpan.FromSeconds(30);
    private readonly IEmailService _emailService;
    private readonly IMemoryCache _cache;
    private readonly ILogger<WebsiteEnquiryController> _logger;

    public WebsiteEnquiryController(IEmailService emailService, IMemoryCache cache, ILogger<WebsiteEnquiryController> logger)
    {
        _emailService = emailService;
        _cache = cache;
        _logger = logger;
    }

    [HttpPost]
    public async Task<IActionResult> Submit([FromBody] WebsiteEnquiryRequest request)
    {
        var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var rateLimitKey = $"website-enquiry:{clientIp}";
        if (_cache.TryGetValue(rateLimitKey, out _))
            return StatusCode(StatusCodes.Status429TooManyRequests, new { success = false, message = "Please wait a moment before submitting another enquiry." });

        _cache.Set(rateLimitKey, true, SubmissionCooldown);
        var submittedAtUtc = DateTime.UtcNow;
        var enquiryId = $"IPQ-{submittedAtUtc:yyyyMMddHHmmss}-{Random.Shared.Next(100, 1000)}";
        var result = await _emailService.SendWebsiteEnquiryAsync(
            request.FullName,
            request.Mobile,
            request.Email,
            request.Interest,
            request.Message,
            enquiryId,
            submittedAtUtc);

        if (result == "1")
            return Ok(new { success = true, message = "Thank you. Our partner team will contact you shortly.", enquiryId });

        _cache.Remove(rateLimitKey);
        _logger.LogError("Website enquiry email failed for {EnquiryId}: {EmailError}", enquiryId, result);
        return StatusCode(StatusCodes.Status500InternalServerError, new { success = false, message = "We could not submit your enquiry right now. Please try again shortly." });
    }
}

using InstantPay.Application.DTOs;
using InstantPay.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InstantPay.API.Controller
{
    [ApiController]
    [Route("api/[controller]")]
    public class WebsiteInfoController : ControllerBase
    {
        private readonly IWebsiteInfoService _websiteInfoService;

        public WebsiteInfoController(IWebsiteInfoService websiteInfoService)
        {
            _websiteInfoService = websiteInfoService;
        }

        [AllowAnonymous]
        [HttpGet("get-info")]
        public async Task<IActionResult> GetWebsiteInfo()
        {
            string? rawUrl = Request.Headers["X-Forwarded-Host"].FirstOrDefault()
                ?? Request.Headers["Host"].FirstOrDefault()
                ?? Request.Host.Host;

            if (string.IsNullOrWhiteSpace(rawUrl))
            {
                return BadRequest(new { message = "Unable to determine domain from request" });
            }

            string domain = rawUrl.Replace("www.", "", StringComparison.OrdinalIgnoreCase);

            var result = await _websiteInfoService.GetWebsiteInfoByDomainAsync(domain);

            if (result == null)
            {
                return NotFound(new { message = "Domain not found or inactive" });
            }

            return Ok(new { data = result });
        }
    }
}

using InstantPay.Application.Interfaces;
using InstantPay.Infrastructure.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace InstantPay.API.Controller
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class DashboardController : ControllerBase
    {
        private readonly IDashboardService _dashboardService;
        private readonly AesEncryptionService _aes;
        public DashboardController(IDashboardService dashboardService, AesEncryptionService aes)
        {
            _dashboardService = dashboardService;
            _aes = aes;
        }

        [HttpGet]
        public async Task<IActionResult> GetDashboard()
        {
            var userIdClaim = User.Claims.FirstOrDefault(c => c.Type == "userid");
            var usernameclaim = User.Claims.FirstOrDefault(c => c.Type == "username");
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
            {
                return Unauthorized(new { message = "Invalid token or user ID." });
            }

            if (usernameclaim == null || string.IsNullOrWhiteSpace(usernameclaim.Value))
            {
                return Unauthorized(new { message = "Invalid token or user ID." });
            }

            var result = await _dashboardService.GetDashboardAsync(userId, usernameclaim.Value);
            var json = JsonSerializer.Serialize(result);
            var encrypted = _aes.Encrypt(json);

            return Ok(new { data = encrypted });
        }
    }
}

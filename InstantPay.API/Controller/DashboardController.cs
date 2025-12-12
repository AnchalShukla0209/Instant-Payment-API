using InstantPay.Application.Interfaces;
using InstantPay.Infrastructure.Security;
using InstantPay.SharedKernel.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace InstantPay.API.Controller
{
    [ApiController]
    [Route("api/[controller]")]
    //[Authorize]
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
            var userId = Request.Headers["userid"].FirstOrDefault();
            var username = Request.Headers["username"].FirstOrDefault();

            if (string.IsNullOrWhiteSpace(userId) || !int.TryParse(userId, out int uid))
                return Unauthorized(new { message = "Invalid or missing userId" });

            if (string.IsNullOrWhiteSpace(username))
                return Unauthorized(new { message = "Invalid or missing username" });

            var result = await _dashboardService.GetDashboardAsync(uid, username);
            var json = JsonSerializer.Serialize(result);
            var encrypted = _aes.Encrypt(json);

            return Ok(new { data = encrypted });
        }

        [HttpGet("GetWalletBalance")]
        public async Task<IActionResult> GetUserBalance([FromQuery] GetWalletBalanceRequest request)
        {
            var userId = Request.Headers["userid"].FirstOrDefault();
            var username = Request.Headers["username"].FirstOrDefault();

            if (string.IsNullOrWhiteSpace(userId) || !int.TryParse(userId, out int uid))
                return Unauthorized(new { message = "Invalid or missing userId" });

            if (string.IsNullOrWhiteSpace(username))
                return Unauthorized(new { message = "Invalid or missing username" });

            var balance = await _dashboardService.GetUserBalance(request);
            if (balance.Balance == 0)
            {
                return NotFound("Wallet balance not found for this user.");
            }

            return Ok(balance);
        }
    }
}

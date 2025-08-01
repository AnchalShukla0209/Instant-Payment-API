using InstantPay.Application.DTOs;
using InstantPay.Application.Interfaces;
using InstantPay.Infrastructure.Security;
using InstantPay.SharedKernel.Entity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace InstantPay.API.Controller
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class MasterController : ControllerBase
    {
        private readonly IMasterService _masterService;
        private readonly AesEncryptionService _aes;
        public MasterController(IMasterService masterService, AesEncryptionService aes)
        {
            _masterService = masterService;
            _aes = aes;
        }

        [HttpPost("GetSuperAdminDashboardData")]
        public async Task<IActionResult> GetSuperAdminDashboardData(EncryptedRequest request)
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
            var decryptedJson = _aes.Decrypt(request.Data);
            var data = JsonSerializer.Deserialize<Superadmindashboardpayload>(decryptedJson);
            var result = await _masterService.GetSuperAdminDashboardData(data.ServiceId, userId, usernameclaim.Value, (int)data.Year);
            var json = JsonSerializer.Serialize(result);
            var encrypted = _aes.Encrypt(json);
            return Ok(new { data = encrypted });
        }

    }
}

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
    //[Authorize]
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
            var userId = Request.Headers["userid"].FirstOrDefault();
            var username = Request.Headers["username"].FirstOrDefault();

            if (string.IsNullOrWhiteSpace(userId) || !int.TryParse(userId, out int uid))
            {
                return Unauthorized(new { message = "Invalid or missing userId" });
            }

            if (string.IsNullOrWhiteSpace(username))
            {
                return Unauthorized(new { message = "Invalid or missing username" });
            }
            var decryptedJson = _aes.Decrypt(request.Data);
            var data = JsonSerializer.Deserialize<Superadmindashboardpayload>(decryptedJson);
            var result = await _masterService.GetSuperAdminDashboardData(data.ServiceId, uid, username, (int)data.Year);
            var json = JsonSerializer.Serialize(result);
            var encrypted = _aes.Encrypt(json);
            return Ok(new { data = encrypted });
        }

        [HttpPost("MasterUserDataForDD")]
        public async Task<IActionResult> GetUserMasterUserDataForDD( string Mode="")
        {
            var userId = Request.Headers["userid"].FirstOrDefault();
            var username = Request.Headers["username"].FirstOrDefault();

            if (string.IsNullOrWhiteSpace(userId) || !int.TryParse(userId, out int uid))
            {
                return Unauthorized(new { message = "Invalid or missing userId" });
            }

            if (string.IsNullOrWhiteSpace(username))
            {
                return Unauthorized(new { message = "Invalid or missing username" });
            }
            var result = await _masterService.GetUserMasterDD(Mode);
            return Ok(result);
        }

        [HttpPost("CheckServiceStatus")]
        public async Task<IActionResult> CheckServiceStatus(string Mode = "", int UserId=0)
        {
            var userId = Request.Headers["userid"].FirstOrDefault();
            var username = Request.Headers["username"].FirstOrDefault();

            if (string.IsNullOrWhiteSpace(userId) || !int.TryParse(userId, out int uid))
            {
                return Unauthorized(new { message = "Invalid or missing userId" });
            }

            if (string.IsNullOrWhiteSpace(username))
            {
                return Unauthorized(new { message = "Invalid or missing username" });
            }
            var result = await _masterService.GetServiceStatus(Mode,UserId);
            return Ok(result);
        }

    }
}

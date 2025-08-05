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
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class ClientController : ControllerBase
    {
        private readonly IClientOperation _reportservice;
        private readonly AesEncryptionService _aes;
        public ClientController(IClientOperation reportservice, AesEncryptionService aes)
        {
            _reportservice = reportservice;
            _aes = aes;
        }

        [HttpPost("Client-Report")]
        public async Task<IActionResult> ClientReport(EncryptedRequest request)
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
            var data = JsonSerializer.Deserialize<GetUsersWithMainBalanceQuery>(decryptedJson);
            var result = await _reportservice.GetClientList(data);
            var json = JsonSerializer.Serialize(result);
            var encrypted = _aes.Encrypt(json);
            return Ok(new { data = encrypted });
        }

    }
}

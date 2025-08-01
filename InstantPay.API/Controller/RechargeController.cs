using Azure;
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
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class RechargeController : ControllerBase
    {
        private readonly IRechargeService _rechargeService;
        private readonly AesEncryptionService _aes;
        public RechargeController(IRechargeService rechargeService, AesEncryptionService aes)
        {
            _rechargeService = rechargeService;
            _aes = aes;
        }

        [HttpPost("submit")]
        public async Task<IActionResult> SubmitRecharge(EncryptedRequest request)
        {
            var decryptedJson = _aes.Decrypt(request.Data);
            var jsonString = JsonSerializer.Deserialize<string>(decryptedJson);

            var dto = JsonSerializer.Deserialize<EncryptedWrapperDto>(jsonString, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
            var result = await _rechargeService.SubmitRechargeAsync(dto.payload);
            var responseJson = JsonSerializer.Serialize(result);
            var encryptedResponse = _aes.Encrypt(responseJson);
            return Ok(new { data = encryptedResponse });

        }


    }
}

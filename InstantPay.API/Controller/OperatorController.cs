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
    public class OperatorController : ControllerBase
    {
        private readonly IOperatorReadRepository _getOperatorsQuery;
        private readonly AesEncryptionService _aes;

        public OperatorController(IOperatorReadRepository getOperatorsQuery, AesEncryptionService aes)
        {
            _getOperatorsQuery = getOperatorsQuery;
            _aes = aes;
        }

        [HttpPost("get-operators")]
        public async Task<IActionResult> GetOperators([FromBody] EncryptedRequestDto request)
        {
            try
            {
                var decryptedServiceName = _aes.Decrypt(request.ServiceName)?.Trim('"');
                var result = await _getOperatorsQuery.GetByServiceNameAsync(decryptedServiceName);
                var encrypted = _aes.Encrypt(JsonSerializer.Serialize(result));
                return Ok(new { data = encrypted });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }
    }
}

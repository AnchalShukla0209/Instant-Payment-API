using Azure;
using InstantPay.Application.DTOs;
using InstantPay.Application.Interfaces;
using InstantPay.Application.Services;
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
    public class ReportController : ControllerBase
    {
        private readonly IReportService _reportservice;
        private readonly AesEncryptionService _aes;
        public ReportController(IReportService reportservice, AesEncryptionService aes)
        {
            _reportservice = reportservice;
            _aes = aes;
        }

        [HttpPost("Txn-Report")]
        public async Task<IActionResult> TxnReport(EncryptedRequest request)
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
            var data = JsonSerializer.Deserialize<TxnReportPayload>(decryptedJson);
            var result = await _reportservice.GetTransactionReportAsync(data.serviceType,data.status,data.dateFrom,data.dateTo,(int)data.userId, (int)data.pageIndex, (int)data.pageSize);
            var json = JsonSerializer.Serialize(result);
            var encrypted = _aes.Encrypt(json);
            return Ok(new { data = encrypted });
        }

        [HttpPost("Get-TxnDetails")]
        public async Task<IActionResult> GetTxnDetails([FromBody] TxnRequest request)
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
            var result = await _reportservice.GetTxnDetails(request.TxnId);
            return Ok(result);
        }

        [HttpPost("Update-TxnStatus")]
        public async Task<IActionResult> UpdateTxnStatus([FromBody] TxnUpdateRequest request)
        {
            var userIdClaim = User.Claims.FirstOrDefault(c => c.Type == "userid");
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int actionById))
            {
                return Unauthorized(new { message = "Invalid token or user ID." });
            }
            var result = await _reportservice.UpdateTxnStatus(request, actionById);
            return Ok(result);
        }


    }
}

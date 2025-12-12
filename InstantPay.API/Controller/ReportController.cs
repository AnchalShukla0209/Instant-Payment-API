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
    //[Authorize]
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
            var data = JsonSerializer.Deserialize<TxnReportPayload>(decryptedJson);
            var result = await _reportservice.GetTransactionReportAsync(data.serviceType,data.status,data.dateFrom,data.dateTo,(int)data.userId, (int)data.pageIndex, (int)data.pageSize);
            var json = JsonSerializer.Serialize(result);
            var encrypted = _aes.Encrypt(json);
            return Ok(new { data = encrypted });
        }

        [HttpPost("Get-TxnDetails")]
        public async Task<IActionResult> GetTxnDetails([FromBody] TxnRequest request)
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
            var result = await _reportservice.GetTxnDetails(request.TxnId);
            return Ok(result);
        }

        [HttpPost("Update-TxnStatus")]
        public async Task<IActionResult> UpdateTxnStatus([FromBody] TxnUpdateRequest request)
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
            var result = await _reportservice.UpdateTxnStatus(request, uid);
            return Ok(result);
        }

        [HttpPost("GetUserTransactionReportAsync")]
        public async Task<IActionResult> GetUserTransactionReportAsync(EncryptedRequest request)
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
            var data = JsonSerializer.Deserialize<TxnReportUserPayload>(decryptedJson);
            var result = await _reportservice.GetUserTransactionReportAsync(data.serviceType, data.status, data.dateFrom, data.dateTo,  (int)data.userId, data.userName, (int)data.pageIndex, (int)data.pageSize);
            var json = JsonSerializer.Serialize(result);
            var encrypted = _aes.Encrypt(json);
            return Ok(new { data = encrypted });
        }


    }
}

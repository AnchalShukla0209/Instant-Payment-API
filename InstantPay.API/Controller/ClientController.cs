using InstantPay.Application.DTOs;
using InstantPay.Application.Interfaces;
using InstantPay.Infrastructure.Security;
using InstantPay.SharedKernel.Entity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace InstantPay.API.Controller
{
    //[Authorize]
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
            var data = JsonSerializer.Deserialize<GetUsersWithMainBalanceQuery>(decryptedJson);
            var result = await _reportservice.GetClientList(data);
            var json = JsonSerializer.Serialize(result);
            var encrypted = _aes.Encrypt(json);
            return Ok(new { data = encrypted });
        }


        [HttpPost("CreateOrUpdateClient")]
        public async Task<IActionResult> CreateOrUpdateClient([FromForm] CreateOrUpdateClientCommand request, CancellationToken cancellationToken)
        {
            var result = await _reportservice.CreateOrUpdateClient(request, cancellationToken);
            return Ok(result);
        }

        [HttpGet("clientId")]
        public async Task<IActionResult> GetClientDetail(int Id)
        {
            var client = await _reportservice.GetClientDetailByIdAsync(Id);

            if (client == null)
                return NotFound("Client not found");

            return Ok(client);
        }

        [HttpDelete("delete-file")]
        public async Task<IActionResult> DeleteClientFile(int clientId, string fileType, CancellationToken cancellationToken)
        {
            var command = new DeleteClientFileCommand
            {
                ClientId = clientId,
                FileType = fileType
            };

            var result = await _reportservice.Handle(command, cancellationToken);
            return Ok(result);
        }

        [HttpPost("wallet-transaction")]
        public async Task<IActionResult> WalletTransaction([FromBody] WalletTransactionRequest request)
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
            var response = await _reportservice.Handle(request);
            return Ok(response);

        }


    }
}

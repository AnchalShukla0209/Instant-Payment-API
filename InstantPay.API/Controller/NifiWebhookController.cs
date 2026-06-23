using InstantPay.Application.Interfaces;
using InstantPay.Infrastructure.Security;
using InstantPay.Infrastructure.Sql.Entities;
using InstantPay.SharedKernel.Entity.NIFIConfigDTO;
using InstantPay.SharedKernel.Results.MoneyTransfer.NIFI;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace InstantPay.API.Controller
{
    [Route("api/[controller]")]
    [ApiController]
    [AllowAnonymous]
    public class WebhookfornifiPayoutController : ControllerBase
    {
        private readonly NIFIConfig _config;
        private readonly AppDbContext _context;
        private readonly IWalletService _walletService;
        private readonly ICommissionService _commissionService;

        private const int ServiceId = 6;
        private const string ApiCode = "NIFI";

        public WebhookfornifiPayoutController(IOptions<NIFIConfig> config, AppDbContext context, IWalletService walletService, ICommissionService commissionService)
        {
            _config = config.Value;
            _context = context;
            _walletService = walletService;
            _commissionService = commissionService;
        }


        private Task DistributeCommissionAsync(
            TransactionDetail tx, TblUser user, decimal amount, int planId)
            => _commissionService.DistributeCommissionAsync(
                tx, user, amount, planId, ServiceId, ApiCode,
                "NIFI Commission Credit");

        [HttpPost]
        public async Task<IActionResult> HandleWebhook([FromBody] NifiEncryptedResponse encryptedResponse)
        {
            try
            {
                string requestJson = JsonConvert.SerializeObject(encryptedResponse);

                if (encryptedResponse == null || string.IsNullOrEmpty(encryptedResponse.body))
                {
                    return BadRequest("Invalid request");
                }

                string decryptedJson = NifiEncryptionService.Decrypt(encryptedResponse.body, _config.EncryptionKey, _config.EncryptionIv);
                var webhookData = JsonConvert.DeserializeObject<NifiWebhookData>(decryptedJson);

                if (webhookData == null)
                {
                    return BadRequest("Decryption failed");
                }

                // Find transaction by ApiTxnId
                var tx = await _context.TransactionDetails
                    .FirstOrDefaultAsync(x => x.ApiTxnId == webhookData.ApiTxnId || x.TxnId == webhookData.ApiTxnId);

                if (tx == null)
                {
                    return NotFound("Transaction not found");
                }

                // Only process if status is Pending
                if (tx.Status?.ToLower() != "pending")
                {
                    return Ok("Transaction already processed");
                }

                string apiStatus = webhookData.status.ToLower().Trim();

                if (apiStatus == "success")
                {
                    tx.Status = "SUCCESS";
                    tx.ApiMsg = apiStatus;
                    tx.UpdateDate = DateTime.Now;
                    tx.Brid = webhookData.data?.BankRefNo ?? "";
                    tx.ApiRes = decryptedJson;
                    await _context.SaveChangesAsync();
                    var user = await _context.TblUsers.FirstOrDefaultAsync(x => x.Id == Convert.ToInt32(tx.UserId));
                    await _context.SaveChangesAsync();
                    if (user != null)
                    {
                        await DistributeCommissionAsync(tx, user, Convert.ToDecimal(tx.Amount), user.CommissionPlanId ?? 1);
                    }
                }
                else if(apiStatus=="failed")
                {
                    tx.Status = "FAILED";
                    tx.ApiMsg = apiStatus;
                    tx.UpdateDate = DateTime.Now;
                    tx.ApiRes = decryptedJson;
                    await _context.SaveChangesAsync();
                    var user = await _context.TblUsers.FirstOrDefaultAsync(x => x.Id == Convert.ToInt32(tx.UserId));
                    decimal rtComm = Convert.ToDecimal(tx.Charge);
                    decimal totalDebit = Convert.ToDecimal(tx.Amount) + rtComm;
                    await _walletService.CreditAsync(
                        user.Id, user.Username + "-" + user.Phone,
                        tx.Amount ?? 0, totalDebit, rtComm, 0,
                        "Money_Transfer_Refund",
                        $"Money Transfer Refunded TXN:{tx.TxnId}",
                        user.Wlid);
                }
                else
                {
                    tx.Status = "PENDING";
                    tx.ApiMsg = apiStatus;
                    tx.UpdateDate = DateTime.Now;
                    tx.ApiRes = decryptedJson;
                    await _context.SaveChangesAsync();
                }

                string responseJson = "Webhook processed successfully";

                // Log to apilogs table
                var log = new Apilog
                {
                    Apiname = "NIFI-Webhook",
                    Reqdatae = DateTime.Now,
                    Request = requestJson,
                    Response = responseJson
                };
                _context.Apilogs.Add(log);
                await _context.SaveChangesAsync();

                return Ok(responseJson);
            }
            catch (Exception ex)
            {
                return StatusCode(500, "Error processing webhook: " + ex.Message);
            }
        }
    }
}

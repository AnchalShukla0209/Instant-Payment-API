using InstantPay.Application.Interfaces;
using InstantPay.Infrastructure.Sql.Entities;
using InstantPay.SharedKernel.Entity.FinzepConfigDTO;
using InstantPay.SharedKernel.Results.MoneyTransfer.Finzep;
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
    public class WebhookforFinzepPayoutController : ControllerBase
    {
        private readonly FinzepConfig _config;
        private readonly AppDbContext _context;
        private readonly IWalletService _walletService;
        private readonly ICommissionService _commissionService;

        private const int ServiceId = 6;
        private const string ApiCode = "FZP";

        public WebhookforFinzepPayoutController(IOptions<FinzepConfig> config, AppDbContext context, IWalletService walletService, ICommissionService commissionService)
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
                $"Commission Credit DMT Payment For Account No {tx.AccountNo}| Credit by Services");

        [HttpPost]
        public async Task<IActionResult> HandleWebhook([FromBody] FinzepWebhookPayload webhookData, CancellationToken cancellationToken)
        {
            try
            {
                string requestJson = JsonConvert.SerializeObject(webhookData);

                if (webhookData == null || string.IsNullOrEmpty(webhookData.rpid))
                {
                    return BadRequest("Invalid request");
                }

                // Find transaction by rpid (stored in ApiTxnId) or TxnId
                var tx = await _context.TransactionDetails
                    .FirstOrDefaultAsync(x => x.ApiTxnId == webhookData.rpid || x.TxnId == webhookData.rpid, cancellationToken);

                if (tx == null)
                {
                    return NotFound("Transaction not found");
                }

                // Only process if status is Pending
                if (tx.Status?.ToLower() != "pending")
                {
                    return Ok("Transaction already processed");
                }

                // status: 2=Success, 1=Pending, 3=Failed, 4=Refunded
                if (webhookData.status == 2)
                {
                    tx.Status = "SUCCESS";
                    tx.ApiMsg = webhookData.msg ?? webhookData.message;
                    tx.UpdateDate = DateTime.Now;
                    tx.Brid = webhookData.liveID ?? webhookData.opid ?? "";
                    tx.ApiRes = requestJson;
                    await _context.SaveChangesAsync(cancellationToken);

                    var user = await _context.TblUsers.FirstOrDefaultAsync(x => x.Id == Convert.ToInt32(tx.UserId), cancellationToken);
                    if (user != null)
                    {
                        // Wallet was pre-debited before the API call in MoneyTransfer.
                        // Only distribute commission here.
                        await DistributeCommissionAsync(tx, user, Convert.ToDecimal(tx.Amount), user.CommissionPlanId ?? 1);
                    }
                }
                else if (webhookData.status == 3)
                {
                    tx.Status = "FAILED";
                    tx.ApiMsg = webhookData.msg ?? webhookData.message;
                    tx.UpdateDate = DateTime.Now;
                    tx.ApiRes = requestJson;
                    tx.Brid = webhookData.liveID ?? webhookData.opid ?? "";
                    await _context.SaveChangesAsync(cancellationToken);

                    var user = await _context.TblUsers.FirstOrDefaultAsync(x => x.Id == Convert.ToInt32(tx.UserId), cancellationToken);
                    if (user != null)
                    {
                        decimal rtComm = Convert.ToDecimal(tx.Charge);
                        decimal totalRefund = Convert.ToDecimal(tx.Amount) + rtComm;

                            await _walletService.CreditAsync(
                            user.Id, user.Username + "-" + user.Phone,
                            tx.Amount ?? 0, totalRefund, rtComm, 0,
                            "Money_Transfer_Refund",
                            $"Money Transfer Refunded | DMT Payment For Account No {tx.AccountNo}| Credit by Services | Refund Credit TXN:{tx.TxnId}",
                            user.Wlid, cancellationToken);
                    }
                }
                else if (webhookData.status == 4)
                {
                    tx.Status = "REFUNDED";
                    tx.ApiMsg = webhookData.msg ?? webhookData.message;
                    tx.UpdateDate = DateTime.Now;
                    tx.ApiRes = requestJson;
                    tx.Brid = webhookData.liveID ?? webhookData.opid ?? "";
                    await _context.SaveChangesAsync(cancellationToken);

                    var user = await _context.TblUsers.FirstOrDefaultAsync(x => x.Id == Convert.ToInt32(tx.UserId), cancellationToken);
                    if (user != null)
                    {
                        decimal rtComm = Convert.ToDecimal(tx.Charge);
                        decimal totalRefund = Convert.ToDecimal(tx.Amount) + rtComm;

                            await _walletService.CreditAsync(
                            user.Id, user.Username + "-" + user.Phone,
                            tx.Amount ?? 0, totalRefund, rtComm, 0,
                            "Money_Transfer_Refund",
                            $"Money Transfer Refunded | DMT Payment For Account No {tx.AccountNo}| Credit by Services | Refund Credit TXN:{tx.TxnId}",
                            user.Wlid, cancellationToken);
                    }
                }
                else
                {
                    tx.Status = "PENDING";
                    tx.ApiMsg = webhookData.msg ?? webhookData.message;
                    tx.UpdateDate = DateTime.Now;
                    tx.ApiRes = requestJson;
                    tx.Brid = webhookData.liveID ?? webhookData.opid ?? "";
                    await _context.SaveChangesAsync(cancellationToken);
                }

                string responseJson = "Webhook processed successfully";

                var log = new Apilog
                {
                    Apiname = "FZP-Webhook",
                    Reqdatae = DateTime.Now,
                    Request = requestJson,
                    Response = responseJson
                };
                _context.Apilogs.Add(log);
                await _context.SaveChangesAsync(cancellationToken);

                return Ok(responseJson);
            }
            catch (Exception ex)
            {
                return StatusCode(500, "Error processing webhook: " + ex.Message);
            }
        }
    }
}

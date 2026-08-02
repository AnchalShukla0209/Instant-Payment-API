using InstantPay.Application.Interfaces;
using InstantPay.Infrastructure.Sql.Entities;
using InstantPay.SharedKernel.Results.MoneyTransfer.AeronPay;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace InstantPay.API.Controller
{
    [Route("api/WebhookforAeronPayPayoutController")]
    [ApiController]
    [AllowAnonymous]
    public class WebhookforAeronPayPayoutController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IWalletService _walletService;
        private readonly ICommissionService _commissionService;

        private const int ServiceId = 6;
        private const string ApiCode = "ARP";

        public WebhookforAeronPayPayoutController(AppDbContext context, IWalletService walletService, ICommissionService commissionService)
        {
            _context = context;
            _walletService = walletService;
            _commissionService = commissionService;
        }

        private Task DistributeCommissionAsync(
            TransactionDetail tx, TblUser user, decimal amount, int planId)
            => _commissionService.DistributeCommissionAsync(
                tx, user, amount, planId, ServiceId, ApiCode,
                $"Commission Credit DMT Payment For Account No {tx.AccountNo}| Credit by Services");

        [AllowAnonymous]
        [HttpPost]
        public async Task<IActionResult> HandleWebhook([FromBody] AeronpayWebhookPayload webhookData, CancellationToken cancellationToken)
        {
            try
            {
                string requestJson = JsonConvert.SerializeObject(webhookData);

                if (webhookData == null || string.IsNullOrEmpty(webhookData.client_referenceId))
                {
                    return BadRequest("Invalid request");
                }

                var webhooklog = new Apilog
                {
                    Apiname = "ARP-Webhook-Log",
                    Reqdatae = DateTime.Now,
                    Request = requestJson,
                    Response = requestJson
                };
                _context.Apilogs.Add(webhooklog);
                await _context.SaveChangesAsync(cancellationToken);

                // ── Settlement Withdrawal callback — checked first ─────────────────────
                var settlement = await _context.SettlementWithdrawals
                    .FirstOrDefaultAsync(x => x.PayoutTransactionId == webhookData.client_referenceId, cancellationToken);

                // ── DMT Transaction lookup ─────────────────────────────────────────────
                var tx = await _context.TransactionDetails
                    .FirstOrDefaultAsync(x => x.TxnId == webhookData.client_referenceId
                                           || x.ApiTxnId == webhookData.client_referenceId
                                           || x.ApiTxnId == webhookData.transactionId, cancellationToken);

                if (tx == null && settlement == null)
                {
                    return NotFound("Transaction not found");
                }

                if (settlement != null)
                {
                    string settlementStatus = webhookData.status?.ToUpper();

                    if (settlementStatus == "FAILED" &&
                        !string.Equals(settlement.PayoutStatus, "FAILED", StringComparison.OrdinalIgnoreCase))
                    {
                        settlement.PayoutStatus = "FAILED";
                        settlement.PayoutResponse = requestJson;
                        await _context.SaveChangesAsync(cancellationToken);

                        var settlementUser = await _context.TblUsers.FirstOrDefaultAsync(
                            x => x.Id == Convert.ToInt32(settlement.UserId), cancellationToken);

                        if (settlementUser != null)
                        {
                            decimal totalRefund = settlement.Amount + settlement.Charge;

                            await _walletService.CreditAsync(
                                settlementUser.Id, settlementUser.Username + "-" + settlementUser.Phone,
                                settlement.Amount, totalRefund, settlement.Charge, 0,
                                "SettlementWithdrawal_Refund",
                                $"Refund for Failed Settlement Withdrawal for AccountNo: {settlement.BankAccount} | Credit by Services | Refund Credit TXN:{settlement.PayoutTransactionId}",
                                settlementUser.Wlid, cancellationToken);
                        }
                    }
                    else if (settlementStatus == "SUCCESS")
                    {
                        settlement.PayoutStatus = "SUCCESS";
                        settlement.PayoutResponse = requestJson;
                        settlement.RRN = webhookData.utr ?? "";
                        await _context.SaveChangesAsync(cancellationToken);
                    }
                    else
                    {
                        settlement.PayoutStatus = settlementStatus;
                        settlement.PayoutResponse = requestJson;
                        await _context.SaveChangesAsync(cancellationToken);
                    }

                    string settlementResponseJson = "Webhook processed successfully";
                    var settlementLog = new Apilog
                    {
                        Apiname = "ARP-Settlement-Webhook",
                        Reqdatae = DateTime.Now,
                        Request = requestJson,
                        Response = settlementResponseJson
                    };
                    _context.Apilogs.Add(settlementLog);
                    await _context.SaveChangesAsync(cancellationToken);

                    return Ok(new { http_status = true, status = "SUCCESS", client_referenceId = webhookData.client_referenceId, statusCode = "200", acknowledged = "1" });
                }

                // ── DMT Transaction callback ───────────────────────────────────────────

                if (tx == null)
                    return NotFound("DMT Transaction not found");

                // Only process if status is Pending
                if (tx.Status?.ToLower() != "pending")
                {
                    return Ok(new { http_status = true, status = "SUCCESS", client_referenceId = webhookData.client_referenceId, statusCode = "200", acknowledged = "1" });
                }

                string status = webhookData.status?.ToUpper();

                if (status == "SUCCESS")
                {
                    tx.Status = "SUCCESS";
                    tx.ApiMsg = webhookData.description;
                    tx.UpdateDate = DateTime.Now;
                    tx.Brid = webhookData.utr ?? "";
                    tx.ApiRes = requestJson;
                    await _context.SaveChangesAsync(cancellationToken);

                    var user = await _context.TblUsers.FirstOrDefaultAsync(x => x.Id == Convert.ToInt32(tx.UserId), cancellationToken);
                    if (user != null)
                    {
                        await DistributeCommissionAsync(tx, user, Convert.ToDecimal(tx.Amount), user.CommissionPlanId ?? 1);
                        await _context.SaveChangesAsync(cancellationToken);
                    }
                }
                else if (status == "FAILED")
                {
                    tx.Status = "FAILED";
                    tx.ApiMsg = webhookData.description;
                    tx.UpdateDate = DateTime.Now;
                    tx.Brid = webhookData.utr ?? "";
                    tx.ApiRes = requestJson;
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
                else if (status == "REFUNDED")
                {
                    tx.Status = "REFUNDED";
                    tx.ApiMsg = webhookData.description;
                    tx.UpdateDate = DateTime.Now;
                    tx.Brid = webhookData.utr ?? "";
                    tx.ApiRes = requestJson;
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
                    tx.ApiMsg = webhookData.description;
                    tx.UpdateDate = DateTime.Now;
                    tx.Brid = webhookData.utr ?? "";
                    tx.ApiRes = requestJson;
                    await _context.SaveChangesAsync(cancellationToken);
                }

                string responseJson = "Webhook processed successfully";

                var log = new Apilog
                {
                    Apiname = "ARP-Webhook",
                    Reqdatae = DateTime.Now,
                    Request = requestJson,
                    Response = responseJson
                };
                _context.Apilogs.Add(log);
                await _context.SaveChangesAsync(cancellationToken);

                return Ok(new { http_status = true, status = "SUCCESS", client_referenceId = webhookData.client_referenceId, statusCode = "200", acknowledged = "1" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, "Error processing webhook: " + ex.Message);
            }
        }
    }
}

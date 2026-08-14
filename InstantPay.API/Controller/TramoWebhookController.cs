using InstantPay.Application.Interfaces;
using InstantPay.Infrastructure.Sql.Entities;
using InstantPay.SharedKernel.Results.MoneyTransfer.Tramo;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace InstantPay.API.Controller
{
    [Route("api/WebhookforTramoPayout")]
    [ApiController]
    [AllowAnonymous]
    public class TramoWebhookController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IWalletService _walletService;
        private readonly ICommissionService _commissionService;

        private const int ServiceId = 6;
        private const string ApiCode = "TRAMO";

        public TramoWebhookController(AppDbContext context, IWalletService walletService, ICommissionService commissionService)
        {
            _context = context;
            _walletService = walletService;
            _commissionService = commissionService;
        }

        private Task DistributeCommissionAsync(TransactionDetail tx, TblUser user, decimal amount, int planId)
            => _commissionService.DistributeCommissionAsync(
                tx, user, amount, planId, ServiceId, ApiCode,
                $"Commission Credit DMT Payment For Account No {tx.AccountNo}| Credit by Services");

        // "Success" → SUCCESS (only when utr is present)
        // "Failed"  → FAILED
        // everything else → PENDING
        private static string MapWebhookStatus(string? tramoStatus, string? utr)
        {
            return (tramoStatus ?? "").ToLower() switch
            {
                "success" => string.IsNullOrWhiteSpace(utr) ? "PENDING" : "SUCCESS",
                "failed"  => "FAILED",
                _         => "PENDING"
            };
        }

        [AllowAnonymous]
        [HttpPost]
        public async Task<IActionResult> HandleWebhook([FromBody] TramoWebhookPayload webhookData, CancellationToken cancellationToken)
        {
            try
            {
                string requestJson = JsonConvert.SerializeObject(webhookData);

                if (webhookData == null ||
                    (string.IsNullOrEmpty(webhookData.partnerTransactionId) && string.IsNullOrEmpty(webhookData.clientRefId)))
                {
                    return BadRequest(new { code = 400, data = new { status = "0", Message = "Invalid request" } });
                }

                var webhooklog = new Apilog
                {
                    Apiname = "TRAMO-Webhook-Log",
                    Reqdatae = DateTime.Now,
                    Request = requestJson,
                    Response = requestJson
                };
                _context.Apilogs.Add(webhooklog);
                await _context.SaveChangesAsync(cancellationToken);

                string pid = webhookData.partnerTransactionId ?? "";
                string utr = webhookData.utr ?? "";
                string mappedStatus = MapWebhookStatus(webhookData.status, utr);

                // ── Settlement Withdrawal callback ─────────────────────────────────────
                var settlement = await _context.SettlementWithdrawals
                    .FirstOrDefaultAsync(x => x.PayoutTransactionId == pid, cancellationToken);

                // ── DMT Transaction lookup ─────────────────────────────────────────────
                var tx = await _context.TransactionDetails
                    .FirstOrDefaultAsync(x => x.TxnId == pid
                                           || x.ApiTxnId == pid
                                           || x.ApiTxnId == webhookData.clientRefId
                                           || x.TxnId == webhookData.clientRefId, cancellationToken);

                if (tx == null && settlement == null)
                {
                    return NotFound(new { code = 404, data = new { status = "0", Message = "Transaction not found" } });
                }

                // ── Settlement processing ──────────────────────────────────────────────
                if (settlement != null)
                {
                    if (mappedStatus == "FAILED" &&
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
                    else if (mappedStatus == "SUCCESS")
                    {
                        settlement.PayoutStatus = "SUCCESS";
                        settlement.PayoutResponse = requestJson;
                        settlement.RRN = utr;
                        await _context.SaveChangesAsync(cancellationToken);
                    }
                    else
                    {
                        settlement.PayoutStatus = mappedStatus;
                        settlement.PayoutResponse = requestJson;
                        await _context.SaveChangesAsync(cancellationToken);
                    }

                    var settlementLog = new Apilog
                    {
                        Apiname = "TRAMO-Settlement-Webhook",
                        Reqdatae = DateTime.Now,
                        Request = requestJson,
                        Response = "Webhook processed successfully"
                    };
                    _context.Apilogs.Add(settlementLog);
                    await _context.SaveChangesAsync(cancellationToken);

                    return Ok(new { code = 200, data = new { status = "1", Message = "Callback Captured Successfully" } });
                }

                // ── DMT Transaction callback ───────────────────────────────────────────

                if (tx == null)
                    return NotFound(new { code = 404, data = new { status = "0", Message = "DMT Transaction not found" } });

                if (tx.Status?.ToLower() != "pending")
                {
                    return Ok(new { code = 200, data = new { status = "1", Message = "Callback Captured Successfully" } });
                }

                if (mappedStatus == "SUCCESS")
                {
                    tx.Status = "SUCCESS";
                    tx.ApiMsg = $"Webhook SUCCESS | utr:{utr} | clientRefId:{webhookData.clientRefId}";
                    tx.UpdateDate = DateTime.Now;
                    tx.Brid = utr;
                    tx.ApiRes = requestJson;
                    await _context.SaveChangesAsync(cancellationToken);

                    var user = await _context.TblUsers.FirstOrDefaultAsync(x => x.Id == Convert.ToInt32(tx.UserId), cancellationToken);
                    if (user != null)
                    {
                        await DistributeCommissionAsync(tx, user, Convert.ToDecimal(tx.Amount), user.CommissionPlanId ?? 1);
                        await _context.SaveChangesAsync(cancellationToken);
                    }
                }
                else if (mappedStatus == "FAILED")
                {
                    tx.Status = "FAILED";
                    tx.ApiMsg = $"Webhook FAILED | clientRefId:{webhookData.clientRefId} | remarks:{webhookData.remarks}";
                    tx.UpdateDate = DateTime.Now;
                    tx.Brid = utr;
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
                    tx.ApiMsg = $"Webhook PENDING | clientRefId:{webhookData.clientRefId} | remarks:{webhookData.remarks}";
                    tx.UpdateDate = DateTime.Now;
                    tx.ApiRes = requestJson;
                    await _context.SaveChangesAsync(cancellationToken);
                }

                var log = new Apilog
                {
                    Apiname = "TRAMO-Webhook",
                    Reqdatae = DateTime.Now,
                    Request = requestJson,
                    Response = "Webhook processed successfully"
                };
                _context.Apilogs.Add(log);
                await _context.SaveChangesAsync(cancellationToken);

                return Ok(new { code = 200, data = new { status = "1", Message = "Callback Captured Successfully" } });
            }
            catch (Exception ex)
            {
                return StatusCode(500, "Error processing webhook: " + ex.Message);
            }
        }
    }
}

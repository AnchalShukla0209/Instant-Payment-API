using InstantPay.Application.Interfaces;
using InstantPay.Infrastructure.Sql.Entities;
using InstantPay.SharedKernel.Results.MoneyTransfer.RechargeKit;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace InstantPay.API.Controller
{
    [Route("api/WebhookforRechargeKitPayPayout")]
    [ApiController]
    [AllowAnonymous]
    public class WebhookforRechargeKitPayPayoutController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IWalletService _walletService;
        private readonly ICommissionService _commissionService;

        private const int ServiceId = 6;
        private const string ApiCode = "RKIT";

        public WebhookforRechargeKitPayPayoutController(AppDbContext context, IWalletService walletService, ICommissionService commissionService)
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

        // status: 1=SUCCESS, 3=FAILURE
        private static string MapWebhookStatus(int status, string opttranid)
        {
            return status switch
            {
                1 => string.IsNullOrWhiteSpace(opttranid) ? "PENDING" : "SUCCESS",
                3 => "FAILED",
                _ => "PENDING"
            };
        }

        private async Task RefundCcBillAsync(TransactionDetail tx, CancellationToken cancellationToken)
        {
            var user = await _context.TblUsers.FirstOrDefaultAsync(x => x.Id == Convert.ToInt32(tx.UserId), cancellationToken);
            if (user == null) return;

            decimal charge = tx.Charge ?? 0;
            decimal gst = tx.Tds ?? 0;
            decimal totalRefund = tx.Cost ?? ((tx.Amount ?? 0) + charge + gst);

            await _walletService.CreditAsync(
                user.Id, user.Name + "-" + user.Phone,
                tx.Amount ?? 0, totalRefund, charge, gst,
                "Credit_Card_Bill_Refund",
                $"Credit Card Bill Refunded | CCBP Payment For Account No {tx.AccountNo}| Credit by Services | Refund Credit TXN:{tx.TxnId}",
                user.Wlid, cancellationToken);
        }

        [AllowAnonymous]
        [HttpPost]
        public async Task<IActionResult> HandleWebhook([FromBody] RechargeKitWebhookPayload webhookData, CancellationToken cancellationToken)
        {
            try
            {
                string requestJson = JsonConvert.SerializeObject(webhookData);

                if (webhookData == null || string.IsNullOrEmpty(webhookData.pid))
                {
                    return BadRequest("Invalid request");
                }

                var webhooklog = new Apilog
                {
                    Apiname = "RKIT-Webhook-Log",
                    Reqdatae = DateTime.Now,
                    Request = requestJson,
                    Response = requestJson
                };
                _context.Apilogs.Add(webhooklog);
                await _context.SaveChangesAsync(cancellationToken);

                // ── Settlement Withdrawal callback ─────────────────────────────────────
                var settlement = await _context.SettlementWithdrawals
                    .FirstOrDefaultAsync(x => x.PayoutTransactionId == webhookData.pid, cancellationToken);

                // ── DMT Transaction lookup ─────────────────────────────────────────────
                var tx = await _context.TransactionDetails
                    .FirstOrDefaultAsync(x => x.TxnId == webhookData.pid
                                           || x.ApiTxnId == webhookData.pid
                                           || x.ApiTxnId == webhookData.orderid, cancellationToken);

                if (tx == null && settlement == null)
                {
                    return NotFound("Transaction not found");
                }

                string mappedStatus = MapWebhookStatus(webhookData.status, webhookData.opttranid ?? "");

                if (settlement != null)
                {
                    string settlementStatus = mappedStatus;

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
                        settlement.RRN = webhookData.opttranid ?? "";
                        await _context.SaveChangesAsync(cancellationToken);
                    }
                    else
                    {
                        settlement.PayoutStatus = settlementStatus;
                        settlement.PayoutResponse = requestJson;
                        await _context.SaveChangesAsync(cancellationToken);
                    }

                    var settlementLog = new Apilog
                    {
                        Apiname = "RKIT-Settlement-Webhook",
                        Reqdatae = DateTime.Now,
                        Request = requestJson,
                        Response = "Webhook processed successfully"
                    };
                    _context.Apilogs.Add(settlementLog);
                    await _context.SaveChangesAsync(cancellationToken);

                    return Ok(new { success = true, pid = webhookData.pid });
                }

                // ── DMT Transaction callback ───────────────────────────────────────────

                if (tx == null)
                    return NotFound("DMT Transaction not found");

                bool isCbill = tx.ServiceId == 3;

                if (tx.Status?.ToLower() != "pending")
                {
                    return Ok(new { success = true, pid = webhookData.pid });
                }

                if (mappedStatus == "SUCCESS")
                {
                    tx.Status = "SUCCESS";
                    tx.ApiMsg = $"Webhook SUCCESS | orderid:{webhookData.orderid}";
                    tx.UpdateDate = DateTime.Now;
                    tx.Brid = webhookData.opttranid ?? "";
                    tx.ApiRes = requestJson;
                    await _context.SaveChangesAsync(cancellationToken);

                    var user = await _context.TblUsers.FirstOrDefaultAsync(x => x.Id == Convert.ToInt32(tx.UserId), cancellationToken);
                    if (user != null && !isCbill)
                    {
                        await DistributeCommissionAsync(tx, user, Convert.ToDecimal(tx.Amount), user.CommissionPlanId ?? 1);
                        await _context.SaveChangesAsync(cancellationToken);
                    }
                }
                else if (mappedStatus == "FAILED")
                {
                    tx.Status = "FAILED";
                    tx.ApiMsg = $"Webhook FAILED | orderid:{webhookData.orderid}";
                    tx.UpdateDate = DateTime.Now;
                    tx.Brid = webhookData.opttranid ?? "";
                    tx.ApiRes = requestJson;
                    await _context.SaveChangesAsync(cancellationToken);

                    var user = await _context.TblUsers.FirstOrDefaultAsync(x => x.Id == Convert.ToInt32(tx.UserId), cancellationToken);
                    if (user != null)
                    {
                        if (isCbill)
                        {
                            await RefundCcBillAsync(tx, cancellationToken);
                        }
                        else
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
                }
                else
                {
                    tx.Status = "PENDING";
                    tx.ApiMsg = $"Webhook PENDING | orderid:{webhookData.orderid}";
                    tx.UpdateDate = DateTime.Now;
                    tx.ApiRes = requestJson;
                    await _context.SaveChangesAsync(cancellationToken);
                }

                var log = new Apilog
                {
                    Apiname = "RKIT-Webhook",
                    Reqdatae = DateTime.Now,
                    Request = requestJson,
                    Response = "Webhook processed successfully"
                };
                _context.Apilogs.Add(log);
                await _context.SaveChangesAsync(cancellationToken);

                return Ok(new { success = true, pid = webhookData.pid });
            }
            catch (Exception ex)
            {
                return StatusCode(500, "Error processing webhook: " + ex.Message);
            }
        }
    }
}

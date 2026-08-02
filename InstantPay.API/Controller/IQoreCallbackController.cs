using InstantPay.Application.Interfaces;
using InstantPay.Infrastructure.Sql.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace InstantPay.API.Controller
{
    [Route("api/[controller]")]
    [ApiController]
    [AllowAnonymous]
    public class IQoreCallbackController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IWalletService _walletService;
        private readonly ICommissionService _commissionService;
        private readonly ILogger<IQoreCallbackController> _logger;

        public IQoreCallbackController(
            AppDbContext context,
            IWalletService walletService,
            ICommissionService commissionService,
            ILogger<IQoreCallbackController> logger)
        {
            _context = context;
            _walletService = walletService;
            _commissionService = commissionService;
            _logger = logger;
        }

        /// <summary>
        /// iQore Back URL callback endpoint.
        /// Register with iQore as: http://yourserver/api/IQoreCallback?tid={tid}&amp;status={status}&amp;opid={opid}
        /// SUCCESS: status=1 | FAILED: status=0
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> HandleCallback(
            [FromQuery] string tid,
            [FromQuery] string status,
            [FromQuery] string opid)
        {
            string requestSummary = $"tid={tid}&status={status}&opid={opid}";
            _logger.LogInformation("iQore Callback received: {Request}", requestSummary);

            try
            {
                if (string.IsNullOrWhiteSpace(tid))
                    return BadRequest("tid is required");

                // Log the callback
                _context.Apilogs.Add(new Apilog
                {
                    Apiname  = "iCore-Callback",
                    Reqdatae = DateTime.Now,
                    Request  = requestSummary,
                    Response = "Processing"
                });
                await _context.SaveChangesAsync();

                var txn = await _context.TransactionDetails
                    .FirstOrDefaultAsync(t => t.TxnId == tid || t.ApiTxnId == tid);

                if (txn == null)
                {
                    _logger.LogWarning("iQore Callback: transaction not found for tid={Tid}", tid);
                    return NotFound("Transaction not found");
                }

                // Idempotency guard — only process PENDING transactions
                if (!string.Equals(txn.Status, "Pending", StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(txn.Status, "PENDING", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogInformation("iQore Callback: transaction {Tid} already in status {Status}, skipping", tid, txn.Status);
                    return Ok("Already processed");
                }

                bool isSuccess = status == "1";
                string mappedStatus = isSuccess ? "SUCCESS" : "FAILED";

                txn.Status    = mappedStatus;
                txn.Brid      = !string.IsNullOrWhiteSpace(opid) && opid != "0" ? opid : txn.Brid;
                txn.ApiMsg    = isSuccess ? "Callback SUCCESS" : "Callback FAILED";
                txn.UpdateDate = DateTime.Now;
                _context.TransactionDetails.Update(txn);
                await _context.SaveChangesAsync();

                var user = await _context.TblUsers.FindAsync(Convert.ToInt32(txn.UserId));

                if (isSuccess)
                {
                    if (user != null)
                    {
                        int planIdForComm  = user.CommissionPlanId ?? (int.TryParse(user.PlanId, out int p) ? p : 1);
                        int commServiceId  = txn.ServiceId ?? 1;
                        int? opIdInt       = int.TryParse(txn.OpId, out int oid) ? oid : (int?)null;
                        const string commApiCode = "ICORE";

                        // Credit retailer commission
                        decimal rtComm = await _commissionService.GetCommissionFromPlanAsync(
                            planIdForComm, txn.Amount ?? 0, commServiceId, commApiCode, "RT", opIdInt);

                        if (rtComm > 0)
                        {
                            await _walletService.CreditAsync(
                                user.Id, $"{user.Name}-{user.Phone}",
                                txn.Amount ?? 0, rtComm, 0, 0,
                                "Commission",
                                $"iQore Commission For TXN {txn.TxnId}",
                                user.Wlid);
                            txn.Comm = rtComm;
                        }

                        // Distribute upline (AD → MD → WL → Admin) differentials
                        await _commissionService.DistributeCommissionAsync(
                            txn, user, txn.Amount ?? 0, planIdForComm,
                            commServiceId, commApiCode,
                            $"iQore Commission Recharge For Account {txn.AccountNo}",
                            opIdInt);

                        _context.TransactionDetails.Update(txn);
                        await _context.SaveChangesAsync();
                    }

                    _logger.LogInformation("iQore Callback: SUCCESS processed for tid={Tid}", tid);
                }
                else
                {
                    // Refund the full face-value amount back to retailer
                    if (user != null)
                    {
                        await _walletService.CreditAsync(
                            user.Id, $"{user.Name}-{user.Phone}",
                            txn.Amount ?? 0, txn.Amount ?? 0, 0, 0,
                            "Recharge Refund",
                            $"iQore Failed Refund For TXN {txn.TxnId}",
                            user.Wlid);
                    }

                    txn.NewBal = Convert.ToString(txn.OldBal);
                    _context.TransactionDetails.Update(txn);
                    await _context.SaveChangesAsync();

                    _logger.LogInformation("iQore Callback: FAILED refund processed for tid={Tid}", tid);
                }

                // Update the api log with result
                _context.Apilogs.Add(new Apilog
                {
                    Apiname  = "iCore-Callback",
                    Reqdatae = DateTime.Now,
                    Request  = requestSummary,
                    Response = mappedStatus
                });
                await _context.SaveChangesAsync();

                return Ok("Callback processed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "iQore Callback error for tid={Tid}", tid);
                return StatusCode(500, "Error processing callback: " + ex.Message);
            }
        }
    }
}

using InstantPay.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InstantPay.API.Controller
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class TransactionRecoveryController : ControllerBase
    {
        private readonly ApiTransactionRecoveryService _recoveryService;

        public TransactionRecoveryController(ApiTransactionRecoveryService recoveryService)
        {
            _recoveryService = recoveryService;
        }

        /// <summary>
        /// Trigger transaction recovery to find and reconcile missing iCore transactions
        /// </summary>
        [HttpPost("recover")]
        public async Task<IActionResult> RecoverMissingTransactions()
        {
            var recoveredCount = await _recoveryService.RecoverMissingTransactions();
            return Ok(new { 
                success = true, 
                message = $"Recovery completed. Found {recoveredCount} missing transactions.",
                recoveredCount = recoveredCount
            });
        }
    }
}

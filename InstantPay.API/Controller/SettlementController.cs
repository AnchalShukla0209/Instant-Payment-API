using InstantPay.Application.DTOs;
using InstantPay.Application.Interfaces;
using InstantPay.SharedKernel.Entity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InstantPay.API.Controller
{
    [ApiController]
    [Route("api/[controller]")]
    public class SettlementController : ControllerBase
    {
        private readonly ISettlementService _settlementService;

        public SettlementController(ISettlementService settlementService)
        {
            _settlementService = settlementService;
        }

        /// <summary>
        /// Get settlement data for the last 2 days (excluding today)
        /// </summary>
        /// <param name="userId">Optional userId to filter by specific user</param>
        /// <returns>Settlement data including AEPS and Razorpay amounts</returns>
        [HttpGet]
        public async Task<ActionResult<SettlementDto>> GetSettlement([FromQuery] string? userId = null)
        {
            try
            {
                int uid = 0;
                string username = null;
                var userIdClaim = User?.FindFirst("userid");
                var usernameClaim = User?.FindFirst("username");

                if (userIdClaim != null &&
                    int.TryParse(userIdClaim.Value, out uid) &&
                    usernameClaim != null &&
                    !string.IsNullOrWhiteSpace(usernameClaim.Value))
                {
                    username = usernameClaim.Value;
                }
                
                if (uid == 0 || string.IsNullOrWhiteSpace(username))
                {
                    var headerUserId = Request.Headers["userid"].FirstOrDefault();
                    var headerUsername = Request.Headers["username"].FirstOrDefault();

                    if (!string.IsNullOrWhiteSpace(headerUserId) &&
                        int.TryParse(headerUserId, out uid) &&
                        !string.IsNullOrWhiteSpace(headerUsername))
                    {
                        username = headerUsername;
                    }
                }

                if (uid == 0 || string.IsNullOrWhiteSpace(username))
                {
                    return Unauthorized(new
                    {
                        message = "Invalid or missing userid/username in token and headers"
                    });
                }

                var settlement = await _settlementService.GetSettlementAsync(userId);
                return Ok(settlement);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error retrieving settlement data", error = ex.Message });
            }
        }

        /// <summary>
        /// Withdraw amount from AEPS or Razorpay settlement
        /// </summary>
        /// <param name="request">Withdrawal request details</param>
        /// <returns>Withdrawal response with remaining balance</returns>
        [HttpPost("withdraw")]
        public async Task<ActionResult<WithdrawalResponseDto>> WithdrawAmount([FromBody] WithdrawalRequestDto request)
        {
            try
            {
                request.ComingFrom = string.IsNullOrEmpty(request.ComingFrom) ? "Web" : request.ComingFrom;
                int uid = 0;
                string username = null;
                var userIdClaim = User?.FindFirst("userid");
                var usernameClaim = User?.FindFirst("username");

                if (userIdClaim != null &&
                    int.TryParse(userIdClaim.Value, out uid) &&
                    usernameClaim != null &&
                    !string.IsNullOrWhiteSpace(usernameClaim.Value))
                {
                    username = usernameClaim.Value;
                }

                if (uid == 0 || string.IsNullOrWhiteSpace(username))
                {
                    var headerUserId = Request.Headers["userid"].FirstOrDefault();
                    var headerUsername = Request.Headers["username"].FirstOrDefault();

                    if (!string.IsNullOrWhiteSpace(headerUserId) &&
                        int.TryParse(headerUserId, out uid) &&
                        !string.IsNullOrWhiteSpace(headerUsername))
                    {
                        username = headerUsername;
                    }
                }

                if (uid == 0 || string.IsNullOrWhiteSpace(username))
                {
                    return Unauthorized(new
                    {
                        message = "Invalid or missing userid/username in token and headers"
                    });
                }

                if (string.IsNullOrEmpty(request.UserId))
                {
                    return BadRequest(new { message = "UserId is required" });
                }

                if (request.Amount <= 0)
                {
                    return BadRequest(new { message = "Amount must be greater than 0" });
                }

                if (string.IsNullOrEmpty(request.WithdrawalType) || 
                    (request.WithdrawalType.ToUpper() != "AEPS" && request.WithdrawalType.ToUpper() != "RAZORPAY" && request.WithdrawalType.ToUpper() != "MATM"))
                {
                    return BadRequest(new { message = "WithdrawalType must be 'AEPS' or 'Razorpay' or 'MATM'" });
                }

                var result = await _settlementService.WithdrawAmountAsync(request);
                
                if (result.Success)
                {
                    return Ok(result);
                }
                else
                {
                    return BadRequest(result);
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error processing withdrawal", error = ex.Message });
            }
        }
    }
}

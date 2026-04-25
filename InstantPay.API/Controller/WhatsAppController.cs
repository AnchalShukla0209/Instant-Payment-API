using InstantPay.Application.Interfaces;
using InstantPay.SharedKernel.RequestPayload.WhatsApp;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InstantPay.API.Controller
{
    [Route("api/[controller]")]
    [ApiController]
    [AllowAnonymous]
    public class WhatsAppController : ControllerBase
    {
        private readonly IWhatsAppService _whatsAppService;
        private readonly ILogger<WhatsAppController> _logger;

        public WhatsAppController(IWhatsAppService whatsAppService, ILogger<WhatsAppController> logger)
        {
            _whatsAppService = whatsAppService;
            _logger = logger;
        }

        /// <summary>
        /// Send a broadcast message to all users via WhatsApp
        /// </summary>
        /// <param name="request">Broadcast message request</param>
        /// <returns>Result of the broadcast operation</returns>
        [HttpPost("broadcast")]
        public async Task<IActionResult> SendBroadcast([FromBody] WhatsAppBroadcastRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(new { success = false, message = "Invalid request data", errors = ModelState });
                }

                _logger.LogInformation("WhatsApp broadcast requested by user");
                var result = await _whatsAppService.SendBroadcastMessageAsync(request);

                if (result.Success)
                {
                    return Ok(new
                    {
                        success = true,
                        message = result.Message,
                        data = new
                        {
                            totalUsers = result.TotalUsers,
                            successfulSends = result.SuccessfulSends,
                            failedSends = result.FailedSends,
                            failedPhoneNumbers = result.FailedPhoneNumbers,
                            sentAt = result.SentAt
                        }
                    });
                }
                else
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = result.Message,
                        data = new
                        {
                            totalUsers = result.TotalUsers,
                            successfulSends = result.SuccessfulSends,
                            failedSends = result.FailedSends
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing WhatsApp broadcast request");
                return StatusCode(500, new
                {
                    success = false,
                    message = "An internal error occurred while processing your request"
                });
            }
        }
    }
}

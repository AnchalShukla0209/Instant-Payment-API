using InstantPay.Application.Interfaces.RazorPay;
using InstantPay.Application.Interfaces;
using InstantPay.SharedKernel.RequestPayload.RazorPay;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using System.Security.Cryptography;
using System.Text;

namespace InstantPay.API.Controller
{
    [ApiController]
    [Route("api/razorpay")]
    public class RazorpayController : ControllerBase
    {
        private readonly IRazorpayService _service;
        private readonly IUserServiceRightService _serviceRightService;
        public RazorpayController(IRazorpayService service, IUserServiceRightService serviceRightService)
        {
            _service = service;
            _serviceRightService = serviceRightService;
        }

        [HttpPost("create-order")]
        public async Task<IActionResult> CreateOrder(CreateOrderRequest request)
        {
            var headerUserId = Request.Headers["userid"].FirstOrDefault();
            if (!int.TryParse(headerUserId, out var authenticatedUserId) || authenticatedUserId != request.UserId)
                return Unauthorized(new { message = "Invalid user session." });

            if (!await _serviceRightService.IsEnabledAsync(authenticatedUserId, "razorpaypayment"))
                return StatusCode(StatusCodes.Status403Forbidden, new { message = "Razorpay Payment is disabled for this user." });

            var result = await _service.CreateOrder(request);
            return Ok(result);
        }

        [HttpPost("verify")]
        public async Task<IActionResult> Verify([FromBody] PaymentVerifyRequest data)
        {
            bool success = await _service.VerifyPayment(
                data.PaymentId.ToString(),
                data.OrderId.ToString(),
                data.Signature.ToString()
            );

            return Ok(new { success });
        }
    }
}

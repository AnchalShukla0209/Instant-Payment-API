using InstantPay.Application.Interfaces.RazorPay;
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
        public RazorpayController(IRazorpayService service)
        {
            _service = service;
        }

        [HttpPost("create-order")]
        public async Task<IActionResult> CreateOrder(CreateOrderRequest request)
        {
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

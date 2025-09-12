using InstantPay.Application.Interfaces;
using InstantPay.Infrastructure.Security;
using InstantPay.SharedKernel.Entity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InstantPay.API.Controller
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class PaymentController : ControllerBase
    {
        private readonly IPaymentService _paymentService;

        public PaymentController(IPaymentService paymentService)
        {
            _paymentService = paymentService;
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromForm] PaymentRequestDto request)
        {
            var userIdClaim = User.Claims.FirstOrDefault(c => c.Type == "userid");
            var usernameclaim = User.Claims.FirstOrDefault(c => c.Type == "username");
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
            {
                return Unauthorized(new { message = "Invalid token or user ID." });
            }
            if (usernameclaim == null || string.IsNullOrWhiteSpace(usernameclaim.Value))
            {
                return Unauthorized(new { message = "Invalid token or user ID." });
            }

            var id = await _paymentService.CreatePaymentRequestAsync(request, userId);
            return CreatedAtAction(nameof(GetById), new { id }, new { PaymentId = id });
        }

        [HttpPut]
        public async Task<IActionResult> Update([FromBody] PaymentUpdateDto request)
        {
            var userIdClaim = User.Claims.FirstOrDefault(c => c.Type == "userid");
            var usernameclaim = User.Claims.FirstOrDefault(c => c.Type == "username");
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
            {
                return Unauthorized(new { message = "Invalid token or user ID." });
            }
            if (usernameclaim == null || string.IsNullOrWhiteSpace(usernameclaim.Value))
            {
                return Unauthorized(new { message = "Invalid token or user ID." });
            }
            await _paymentService.UpdatePaymentAsync(request);
            return NoContent();
        }

        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10,
            [FromQuery] string status = null, [FromQuery] DateTime? fromDate = null, [FromQuery] DateTime? toDate = null)
        {
            var userIdClaim = User.Claims.FirstOrDefault(c => c.Type == "userid");
            var usernameclaim = User.Claims.FirstOrDefault(c => c.Type == "username");
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
            {
                return Unauthorized(new { message = "Invalid token or user ID." });
            }
            if (usernameclaim == null || string.IsNullOrWhiteSpace(usernameclaim.Value))
            {
                return Unauthorized(new { message = "Invalid token or user ID." });
            }
            var result = await _paymentService.GetAllPaymentsAsync(pageNumber, pageSize, status, fromDate, toDate);
            return Ok(new { result.Payments, result.TotalCount });
        }

        [HttpGet("download/{id}")]
        public async Task<IActionResult> Download(Guid id)
        {
            var userIdClaim = User.Claims.FirstOrDefault(c => c.Type == "userid");
            var usernameclaim = User.Claims.FirstOrDefault(c => c.Type == "username");
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
            {
                return Unauthorized(new { message = "Invalid token or user ID." });
            }
            if (usernameclaim == null || string.IsNullOrWhiteSpace(usernameclaim.Value))
            {
                return Unauthorized(new { message = "Invalid token or user ID." });
            }
            var file = await _paymentService.DownloadTxnSlipAsync(id);
            return File(file.FileContent, file.ContentType, file.FileName);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            var userIdClaim = User.Claims.FirstOrDefault(c => c.Type == "userid");
            var usernameclaim = User.Claims.FirstOrDefault(c => c.Type == "username");
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
            {
                return Unauthorized(new { message = "Invalid token or user ID." });
            }
            if (usernameclaim == null || string.IsNullOrWhiteSpace(usernameclaim.Value))
            {
                return Unauthorized(new { message = "Invalid token or user ID." });
            }
            var result = await _paymentService.DownloadTxnSlipAsync(id);
            return Ok(result);
        }


    }
}

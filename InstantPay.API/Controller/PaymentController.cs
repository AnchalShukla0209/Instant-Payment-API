using InstantPay.Application.Interfaces;
using InstantPay.Infrastructure.Security;
using InstantPay.SharedKernel.Entity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InstantPay.API.Controller
{
    //[Authorize]
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
            try
            {
                var userId = Request.Headers["userid"].FirstOrDefault();
                var username = Request.Headers["username"].FirstOrDefault();

                if (string.IsNullOrWhiteSpace(userId) || !int.TryParse(userId, out int uid))
                {
                    return Unauthorized(new { message = "Invalid or missing userId" });
                }

                if (string.IsNullOrWhiteSpace(username))
                {
                    return Unauthorized(new { message = "Invalid or missing username" });
                }

                var id = await _paymentService.CreatePaymentRequestAsync(request, uid);
                return Ok(id);
            }
            catch (ArgumentException ae)
            {
                return BadRequest(new { Success = false, Message = ae.Message });
            }
            catch (IOException ex)
            {

                return BadRequest(new { Success = false, Message = ex.Message });
            }
            catch (Exception ae)
            {
                return BadRequest(new { Success = false, Message = ae.Message });
            }
            
        }

        [HttpPut]
        public async Task<IActionResult> Update([FromBody] PaymentUpdateDto request)
        {
            try
            {
                var userId = Request.Headers["userid"].FirstOrDefault();
                var username = Request.Headers["username"].FirstOrDefault();

                if (string.IsNullOrWhiteSpace(userId) || !int.TryParse(userId, out int uid))
                {
                    return Unauthorized(new { message = "Invalid or missing userId" });
                }

                if (string.IsNullOrWhiteSpace(username))
                {
                    return Unauthorized(new { message = "Invalid or missing username" });
                }
                await _paymentService.UpdatePaymentAsync(request);
                return NoContent();
            }
            catch (ArgumentException ae)
            {
                return BadRequest(new { Success = false, Message = ae.Message });
            }
            catch (IOException ex)
            {

                return BadRequest(new { Success = false, Message = ex.Message });
            }
            catch (Exception ae)
            {
                return BadRequest(new { Success = false, Message = ae.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10,
            [FromQuery] string status = null, [FromQuery] DateTime? fromDate = null, [FromQuery] DateTime? toDate = null)
        {
            try
            {
                var userId = Request.Headers["userid"].FirstOrDefault();
                var username = Request.Headers["username"].FirstOrDefault();

                if (string.IsNullOrWhiteSpace(userId) || !int.TryParse(userId, out int uid))
                {
                    return Unauthorized(new { message = "Invalid or missing userId" });
                }

                if (string.IsNullOrWhiteSpace(username))
                {
                    return Unauthorized(new { message = "Invalid or missing username" });
                }
                var result = await _paymentService.GetAllPaymentsAsync(pageNumber, pageSize, status, fromDate, toDate);
                return Ok(new { result.Payments, result.TotalCount });
            }
            catch (ArgumentException ae)
            {
                return BadRequest(new { Success = false, Message = ae.Message });
            }
            catch (IOException ex)
            {

                return BadRequest(new { Success = false, Message = ex.Message });
            }
            catch (Exception ae)
            {
                return BadRequest(new { Success = false, Message = ae.Message });
            }
        }

        [HttpGet("download/{id}")]
        public async Task<IActionResult> Download(Guid id)
        {
            try
            {
                var userId = Request.Headers["userid"].FirstOrDefault();
                var username = Request.Headers["username"].FirstOrDefault();

                if (string.IsNullOrWhiteSpace(userId) || !int.TryParse(userId, out int uid))
                {
                    return Unauthorized(new { message = "Invalid or missing userId" });
                }

                if (string.IsNullOrWhiteSpace(username))
                {
                    return Unauthorized(new { message = "Invalid or missing username" });
                }
                var file = await _paymentService.DownloadTxnSlipAsync(id);
                return File(file.FileContent, file.ContentType, file.FileName);
            }
            catch (FileNotFoundException ex)
            {

                return BadRequest(new { Success = false, Message = ex.Message });
            }

            catch (ArgumentException ae)
            {
                return BadRequest(new { Success = false, Message = ae.Message });
            }
            catch (IOException ex)
            {

                return BadRequest(new { Success = false, Message = ex.Message });
            }
           
            catch (Exception ae)
            {
                return BadRequest(new { Success = false, Message = ae.Message });
            }

        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            try
            {
                var userId = Request.Headers["userid"].FirstOrDefault();
                var username = Request.Headers["username"].FirstOrDefault();

                if (string.IsNullOrWhiteSpace(userId) || !int.TryParse(userId, out int uid))
                {
                    return Unauthorized(new { message = "Invalid or missing userId" });
                }

                if (string.IsNullOrWhiteSpace(username))
                {
                    return Unauthorized(new { message = "Invalid or missing username" });
                }
                var result = await _paymentService.GetPaymentByIdAsync(id);
                return Ok(result);
            }
            catch (ArgumentException ae)
            {
                return BadRequest(new { Success = false, Message = ae.Message });
            }
            catch (IOException ex)
            {

                return BadRequest(new { Success = false, Message = ex.Message });
            }
            catch (Exception ae)
            {
                return BadRequest(new { Success = false, Message = ae.Message });
            }
        }


    }
}

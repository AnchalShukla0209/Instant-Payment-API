using InstantPay.Application.Interfaces;
using InstantPay.Infrastructure.Security;
using InstantPay.SharedKernel.Entity;
using InstantPay.SharedKernel.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InstantPay.API.Controller
{
    //[Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class BankController : ControllerBase
    {
        private readonly IBankRepository _bankService;

        public BankController(IBankRepository bankService)
        {
            _bankService = bankService;

        }


        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10)
        {
            var userId = Request.Headers["userid"].FirstOrDefault();
            var username = Request.Headers["username"].FirstOrDefault();

            if (string.IsNullOrWhiteSpace(userId) || !int.TryParse(userId, out int uid))
                return Unauthorized(new { message = "Invalid or missing userId" });

            if (string.IsNullOrWhiteSpace(username))
                return Unauthorized(new { message = "Invalid or missing username" });
            var result = await _bankService.GetAllAsync(pageNumber, pageSize);
            return Ok(result);
        }

        [HttpGet("active")]
        public async Task<IActionResult> GetAllActive()
        {
            var userId = Request.Headers["userid"].FirstOrDefault();
            var username = Request.Headers["username"].FirstOrDefault();

            if (string.IsNullOrWhiteSpace(userId) || !int.TryParse(userId, out int uid))
                return Unauthorized(new { message = "Invalid or missing userId" });

            if (string.IsNullOrWhiteSpace(username))
                return Unauthorized(new { message = "Invalid or missing username" });

            var result = await _bankService.GetAllActiveAsync();
            return Ok(result);
        }


        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            var userId = Request.Headers["userid"].FirstOrDefault();
            var username = Request.Headers["username"].FirstOrDefault();

            if (string.IsNullOrWhiteSpace(userId) || !int.TryParse(userId, out int uid))
                return Unauthorized(new { message = "Invalid or missing userId" });

            if (string.IsNullOrWhiteSpace(username))
                return Unauthorized(new { message = "Invalid or missing username" });

            var bank = await _bankService.GetByIdAsync(id);
            return bank == null ? NotFound() : Ok(bank);
        }


        [HttpPost]
        public async Task<IActionResult> Create([FromBody] BankDto bank)
        {
            try
            {
                var userId = Request.Headers["userid"].FirstOrDefault();
                var username = Request.Headers["username"].FirstOrDefault();

                if (string.IsNullOrWhiteSpace(userId) || !int.TryParse(userId, out int uid))
                    return Unauthorized(new { message = "Invalid or missing userId" });

                if (string.IsNullOrWhiteSpace(username))
                    return Unauthorized(new { message = "Invalid or missing username" });

                var id = await _bankService.CreateAsync(bank, uid);
                return CreatedAtAction(nameof(GetById), new { id }, bank);
            }
            catch (Exception ex)
            {
                return BadRequest(new { Success = false, Message = ex.Message });
            }
        }


        [HttpPut("{id}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] BankDto bank)
        {
            try
            {

                var userId = Request.Headers["userid"].FirstOrDefault();
                var username = Request.Headers["username"].FirstOrDefault();

                if (string.IsNullOrWhiteSpace(userId) || !int.TryParse(userId, out int uid))
                    return Unauthorized(new { message = "Invalid or missing userId" });

                if (string.IsNullOrWhiteSpace(username))
                    return Unauthorized(new { message = "Invalid or missing username" });

                bank.BankId = id;
                await _bankService.UpdateAsync(bank, uid);
                return NoContent();
            }
            catch (Exception ex)
            {
                return BadRequest(new { Success = false, Message = ex.Message });
            }
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            try
            {
                var userId = Request.Headers["userid"].FirstOrDefault();
                var username = Request.Headers["username"].FirstOrDefault();

                if (string.IsNullOrWhiteSpace(userId) || !int.TryParse(userId, out int uid))
                    return Unauthorized(new { message = "Invalid or missing userId" });

                if (string.IsNullOrWhiteSpace(username))
                    return Unauthorized(new { message = "Invalid or missing username" });

                await _bankService.DeleteAsync(id, uid);
                return NoContent();
            }
            catch (Exception ex)
            {
                return BadRequest(new { Success = false, Message = ex.Message });
            }
        }

        [HttpGet("BankListForJPB")]
        public async Task<IActionResult> GetBankDropdown()
        {
            var userId = Request.Headers["userid"].FirstOrDefault();
            var username = Request.Headers["username"].FirstOrDefault();

            if (string.IsNullOrWhiteSpace(userId) || !int.TryParse(userId, out int uid))
                return Unauthorized(new { message = "Invalid or missing userId" });

            if (string.IsNullOrWhiteSpace(username))
                return Unauthorized(new { message = "Invalid or missing username" });

            var banks = await _bankService.GetBankListForJPB();
             
            return Ok(new { status = true, data = banks });
        }



    }
}

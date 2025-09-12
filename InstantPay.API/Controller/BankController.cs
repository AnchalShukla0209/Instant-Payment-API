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
    public class BankController : ControllerBase
    {
        private readonly IBankRepository _bankService;
        //private readonly AesEncryptionService _aes;

        public BankController(IBankRepository bankService/*, AesEncryptionService aes*/)
        {
            _bankService = bankService;
           // _aes = aes;
        }


        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10)
        {
           
            var userIdClaim = User.Claims.FirstOrDefault(c => c.Type == "userid");
            var usernameClaim = User.Claims.FirstOrDefault(c => c.Type == "username");

            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
            {
                return Unauthorized(new { message = "Invalid token or user ID." });
            }

            if (usernameClaim == null || string.IsNullOrWhiteSpace(usernameClaim.Value))
            {
                return Unauthorized(new { message = "Invalid token or username." });
            }

            var result = await _bankService.GetAllAsync(pageNumber, pageSize);
            return Ok(result);
        }

        [HttpGet("active")]
        public async Task<IActionResult> GetAllActive()
        {
            var userIdClaim = User.Claims.FirstOrDefault(c => c.Type == "userid");
            var usernameClaim = User.Claims.FirstOrDefault(c => c.Type == "username");

            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
            {
                return Unauthorized(new { message = "Invalid token or user ID." });
            }

            if (usernameClaim == null || string.IsNullOrWhiteSpace(usernameClaim.Value))
            {
                return Unauthorized(new { message = "Invalid token or username." });
            }

            var result = await _bankService.GetAllActiveAsync();
            return Ok(result);
        }


        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            var userIdClaim = User.Claims.FirstOrDefault(c => c.Type == "userid");
            var usernameClaim = User.Claims.FirstOrDefault(c => c.Type == "username");

            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
            {
                return Unauthorized(new { message = "Invalid token or user ID." });
            }

            if (usernameClaim == null || string.IsNullOrWhiteSpace(usernameClaim.Value))
            {
                return Unauthorized(new { message = "Invalid token or username." });
            }

            var bank = await _bankService.GetByIdAsync(id);
            return bank == null ? NotFound() : Ok(bank);
        }


        [HttpPost]
        public async Task<IActionResult> Create([FromBody] BankDto bank)
        {
            var userIdClaim = User.Claims.FirstOrDefault(c => c.Type == "userid");
            var usernameClaim = User.Claims.FirstOrDefault(c => c.Type == "username");

            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
            {
                return Unauthorized(new { message = "Invalid token or user ID." });
            }

            if (usernameClaim == null || string.IsNullOrWhiteSpace(usernameClaim.Value))
            {
                return Unauthorized(new { message = "Invalid token or username." });
            }

            var id = await _bankService.CreateAsync(bank);
            return CreatedAtAction(nameof(GetById), new { id }, bank);
        }


        [HttpPut("{id}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] BankDto bank)
        {
            var userIdClaim = User.Claims.FirstOrDefault(c => c.Type == "userid");
            var usernameClaim = User.Claims.FirstOrDefault(c => c.Type == "username");

            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
            {
                return Unauthorized(new { message = "Invalid token or user ID." });
            }

            if (usernameClaim == null || string.IsNullOrWhiteSpace(usernameClaim.Value))
            {
                return Unauthorized(new { message = "Invalid token or username." });
            }

            bank.BankId = id;
            await _bankService.UpdateAsync(bank);
            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var userIdClaim = User.Claims.FirstOrDefault(c => c.Type == "userid");
            var usernameClaim = User.Claims.FirstOrDefault(c => c.Type == "username");

            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
            {
                return Unauthorized(new { message = "Invalid token or user ID." });
            }

            if (usernameClaim == null || string.IsNullOrWhiteSpace(usernameClaim.Value))
            {
                return Unauthorized(new { message = "Invalid token or username." });
            }

            await _bankService.DeleteAsync(id);
            return NoContent();
        }



    }
}

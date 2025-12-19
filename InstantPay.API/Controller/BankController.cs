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
            int uid = 0;
            string username = null;

            // 1️⃣ Try JWT claims FIRST
            var userIdClaim = User?.FindFirst("userid");
            var usernameClaim = User?.FindFirst("username");

            if (userIdClaim != null &&
                int.TryParse(userIdClaim.Value, out uid) &&
                usernameClaim != null &&
                !string.IsNullOrWhiteSpace(usernameClaim.Value))
            {
                username = usernameClaim.Value;
            }

            // 2️⃣ If JWT not available → fallback to Headers
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

            // 3️⃣ Unauthorized ONLY if both sources failed
            if (uid == 0 || string.IsNullOrWhiteSpace(username))
            {
                return Unauthorized(new
                {
                    message = "Invalid or missing userid/username in token and headers"
                });
            }
            var result = await _bankService.GetAllAsync(pageNumber, pageSize);
            return Ok(result);
        }

        [HttpGet("active")]
        public async Task<IActionResult> GetAllActive()
        {
            int uid = 0;
            string username = null;

            // 1️⃣ Try JWT claims FIRST
            var userIdClaim = User?.FindFirst("userid");
            var usernameClaim = User?.FindFirst("username");

            if (userIdClaim != null &&
                int.TryParse(userIdClaim.Value, out uid) &&
                usernameClaim != null &&
                !string.IsNullOrWhiteSpace(usernameClaim.Value))
            {
                username = usernameClaim.Value;
            }

            // 2️⃣ If JWT not available → fallback to Headers
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

            // 3️⃣ Unauthorized ONLY if both sources failed
            if (uid == 0 || string.IsNullOrWhiteSpace(username))
            {
                return Unauthorized(new
                {
                    message = "Invalid or missing userid/username in token and headers"
                });
            }

            var result = await _bankService.GetAllActiveAsync();
            return Ok(result);
        }


        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            int uid = 0;
            string username = null;

            // 1️⃣ Try JWT claims FIRST
            var userIdClaim = User?.FindFirst("userid");
            var usernameClaim = User?.FindFirst("username");

            if (userIdClaim != null &&
                int.TryParse(userIdClaim.Value, out uid) &&
                usernameClaim != null &&
                !string.IsNullOrWhiteSpace(usernameClaim.Value))
            {
                username = usernameClaim.Value;
            }

            // 2️⃣ If JWT not available → fallback to Headers
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

            // 3️⃣ Unauthorized ONLY if both sources failed
            if (uid == 0 || string.IsNullOrWhiteSpace(username))
            {
                return Unauthorized(new
                {
                    message = "Invalid or missing userid/username in token and headers"
                });
            }

            var bank = await _bankService.GetByIdAsync(id);
            return bank == null ? NotFound() : Ok(bank);
        }


        [HttpPost]
        public async Task<IActionResult> Create([FromBody] BankDto bank)
        {
            try
            {
                int uid = 0;
                string username = null;

                // 1️⃣ Try JWT claims FIRST
                var userIdClaim = User?.FindFirst("userid");
                var usernameClaim = User?.FindFirst("username");

                if (userIdClaim != null &&
                    int.TryParse(userIdClaim.Value, out uid) &&
                    usernameClaim != null &&
                    !string.IsNullOrWhiteSpace(usernameClaim.Value))
                {
                    username = usernameClaim.Value;
                }

                // 2️⃣ If JWT not available → fallback to Headers
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

                // 3️⃣ Unauthorized ONLY if both sources failed
                if (uid == 0 || string.IsNullOrWhiteSpace(username))
                {
                    return Unauthorized(new
                    {
                        message = "Invalid or missing userid/username in token and headers"
                    });
                }

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

                int uid = 0;
                string username = null;

                // 1️⃣ Try JWT claims FIRST
                var userIdClaim = User?.FindFirst("userid");
                var usernameClaim = User?.FindFirst("username");

                if (userIdClaim != null &&
                    int.TryParse(userIdClaim.Value, out uid) &&
                    usernameClaim != null &&
                    !string.IsNullOrWhiteSpace(usernameClaim.Value))
                {
                    username = usernameClaim.Value;
                }

                // 2️⃣ If JWT not available → fallback to Headers
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

                // 3️⃣ Unauthorized ONLY if both sources failed
                if (uid == 0 || string.IsNullOrWhiteSpace(username))
                {
                    return Unauthorized(new
                    {
                        message = "Invalid or missing userid/username in token and headers"
                    });
                }

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
                int uid = 0;
                string username = null;

                // 1️⃣ Try JWT claims FIRST
                var userIdClaim = User?.FindFirst("userid");
                var usernameClaim = User?.FindFirst("username");

                if (userIdClaim != null &&
                    int.TryParse(userIdClaim.Value, out uid) &&
                    usernameClaim != null &&
                    !string.IsNullOrWhiteSpace(usernameClaim.Value))
                {
                    username = usernameClaim.Value;
                }

                // 2️⃣ If JWT not available → fallback to Headers
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

                // 3️⃣ Unauthorized ONLY if both sources failed
                if (uid == 0 || string.IsNullOrWhiteSpace(username))
                {
                    return Unauthorized(new
                    {
                        message = "Invalid or missing userid/username in token and headers"
                    });
                }

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
            int uid = 0;
            string username = null;

            // 1️⃣ Try JWT claims FIRST
            var userIdClaim = User?.FindFirst("userid");
            var usernameClaim = User?.FindFirst("username");

            if (userIdClaim != null &&
                int.TryParse(userIdClaim.Value, out uid) &&
                usernameClaim != null &&
                !string.IsNullOrWhiteSpace(usernameClaim.Value))
            {
                username = usernameClaim.Value;
            }

            // 2️⃣ If JWT not available → fallback to Headers
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

            // 3️⃣ Unauthorized ONLY if both sources failed
            if (uid == 0 || string.IsNullOrWhiteSpace(username))
            {
                return Unauthorized(new
                {
                    message = "Invalid or missing userid/username in token and headers"
                });
            }

            var banks = await _bankService.GetBankListForJPB();
             
            return Ok(new { status = true, data = banks });
        }



    }
}

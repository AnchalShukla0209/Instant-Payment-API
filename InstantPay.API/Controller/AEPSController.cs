using InstantPay.Application.Interfaces;
using InstantPay.Application.Services;
using InstantPay.SharedKernel.RequestPayload;
using InstantPay.SharedKernel.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace InstantPay.API.Controller
{
    [Route("api/[controller]")]
    [ApiController]
    public class AEPSController : ControllerBase
    {
        private readonly IAEPSService _service;
        private readonly IJPBBalanceEnquiry _balanceEnquiryService;
        private readonly IJPBMiniStatement _miniStatementService;
        private readonly IJPPCashWithdrawal _cashwithdrawal;
        public AEPSController(IAEPSService service, IJPBBalanceEnquiry balanceEnquiryService, IJPBMiniStatement miniStatementService, IJPPCashWithdrawal cashwithdrawal)
        {
            _service = service;
            _balanceEnquiryService = balanceEnquiryService;
            _miniStatementService = miniStatementService;
            _cashwithdrawal = cashwithdrawal;
        }

        [HttpGet("agentstatus")]
        public async Task<IActionResult> AgentStatus(string agentId, string mob, string aadharnumber)
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


            var result = await _service.GetJioAgentAEPSLogin(agentId, mob, aadharnumber);
            return Ok(result);
        }

        [HttpGet("CheckAgentDailyLogin")]
        public async Task<IActionResult> CheckAgentDailyLogin()
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

            var result = await _service.CheckAgentDailyLoginAsync(uid);
            if (result.StatusCode == "401")
            {
                return Unauthorized(new { message = "Invalid or missing username" });
            }
            return Ok(result);
        }

        [HttpPost("createAgent")]
        public async Task<IActionResult> CreateAgent([FromBody] CreateAgentRequestDto request)
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

            var res = await _service.CreateAgentAsync(request);
            return Ok(res);
        }

        [HttpPost("AgentEKYC")]
        public async Task<IActionResult> ExecuteAsync([FromBody] AgentEKYCRequestDto request)
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

            var res = await _service.AgentEKYCAsync(request);
            return Ok(res);
        }

        [HttpPost("JPBBalanceEnquiry")]
        public async Task<IActionResult> JPBBalanceEnquiry([FromBody] BalanceInquiryRequestDto request)
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

            request.UserId = Convert.ToString(uid);
            var res = await _balanceEnquiryService.BalanceInquiryAsync(request);
            return Ok(res);
        }

        [HttpPost("JPBMiniStatement")]
        public async Task<IActionResult> JPBMiniStatement([FromBody] MiniStatementRequest request)
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

            request.UserId = Convert.ToString(uid);
            var res = await _miniStatementService.MiniStatementAsync(request);
            return Ok(res);
        }

        [HttpPost("JPBCashWithdrawal")]
        public async Task<IActionResult> JPBCashWithdrawal([FromBody] CashWithdrawalRequest request)
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

            request.UserId = Convert.ToString(uid);
            var res = await _cashwithdrawal.CashWithdrawalAsync(request);
            return Ok(res);
        }
    }
}

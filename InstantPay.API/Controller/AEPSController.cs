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
            var result = await _service.GetJioAgentAEPSLogin(agentId, mob, aadharnumber);
            return Ok(result);
        }

        [HttpGet("CheckAgentDailyLogin")]
        public async Task<IActionResult> CheckAgentDailyLogin()
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
            var result = await _service.CheckAgentDailyLoginAsync(uid);
            if(result.StatusCode=="401")
            {
                return Unauthorized(new { message = "Invalid or missing username" });
            }
            return Ok(result);
        }

        [HttpPost("createAgent")]
        public async Task<IActionResult> CreateAgent([FromBody] CreateAgentRequestDto request)
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
            var res = await _service.CreateAgentAsync(request);
            return Ok(res);
        }

        [HttpPost("AgentEKYC")]
        public async Task<IActionResult> ExecuteAsync([FromBody] AgentEKYCRequestDto request)
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
            var res = await _service.AgentEKYCAsync(request);
            return Ok(res);
        }

        [HttpPost("JPBBalanceEnquiry")]
        public async Task<IActionResult> JPBBalanceEnquiry([FromBody] BalanceInquiryRequestDto request)
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
            request.UserId = Convert.ToString(uid);
            var res = await _balanceEnquiryService.BalanceInquiryAsync(request);
            return Ok(res);
        }

        [HttpPost("JPBMiniStatement")]
        public async Task<IActionResult> JPBMiniStatement([FromBody] MiniStatementRequest request)
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
            request.UserId = Convert.ToString(uid);
            var res = await _miniStatementService.MiniStatementAsync(request);
            return Ok(res);
        }

        [HttpPost("JPBCashWithdrawal")]
        public async Task<IActionResult> JPBCashWithdrawal([FromBody] CashWithdrawalRequest request)
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
            request.UserId = Convert.ToString(uid);
            var res = await _cashwithdrawal.CashWithdrawalAsync(request);
            return Ok(res);
        }
    }
}

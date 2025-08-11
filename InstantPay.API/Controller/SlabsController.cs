using InstantPay.Application.Interfaces;
using InstantPay.Application.Services;
using InstantPay.SharedKernel.Entity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace InstantPay.API.Controller
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class SlabsController : ControllerBase
    {
        private readonly ISlabReadRepository _mediator;
        public SlabsController(ISlabReadRepository mediator) => _mediator = mediator;

        [HttpGet("GetMargin")]
        public async Task<ActionResult<PagedResult<SlabInfoDto>>> GetMargin([FromQuery] string? serviceName, [FromQuery] int pageIndex = 1, [FromQuery] int pageSize = 50)
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
            var result = await _mediator.GetSlabInfoAsync(serviceName, pageIndex, pageSize);
            return Ok(result);
        }

        [HttpPost("update")]
        public async Task<IActionResult> Update([FromBody] UpdateCommissionCommand command)
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
            var result = await _mediator.Handle(command);
            return Ok(result);
        }
    }
}

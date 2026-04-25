using InstantPay.Application.Interfaces.PAN;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace InstantPay.API.Controller
{
    [ApiController]
    [Route("api/[controller]")]
    public class PanController : ControllerBase
    {
        private readonly IPanService _panService;

        public PanController(IPanService panService)
        {
            _panService = panService;
        }

        [HttpGet("verify")]
        public async Task<IActionResult> VerifyPan([FromQuery] string panNumber)
        {
            if (string.IsNullOrWhiteSpace(panNumber))
                return BadRequest("PAN is required");

            var result = await _panService.VerifyPanAsync(panNumber);

            return Ok(result);
        }
    }
}

using InstantPay.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InstantPay.API.Controller
{
    [ApiController]
    [Route("api/[controller]")]
    public class APICodeController : ControllerBase
    {
        private readonly IAPICodeService _apiCodeService;

        public APICodeController(IAPICodeService apiCodeService)
        {
            _apiCodeService = apiCodeService;
        }

        [HttpGet("dropdown")]
        public async Task<IActionResult> GetAPICodesForDropdown()
        {
            try
            {
                var result = await _apiCodeService.GetAPICodesForDropdown();
                return Ok(new { success = true, data = result });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }
    }
}

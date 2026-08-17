using InstantPay.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace InstantPay.API.Controller;

[ApiController]
[Route("api/[controller]")]
public sealed class UserDropdownController : ControllerBase
{
    private readonly IUserDropdownService _userDropdownService;

    public UserDropdownController(IUserDropdownService userDropdownService)
    {
        _userDropdownService = userDropdownService;
    }

    [HttpGet("wl")]
    public async Task<IActionResult> GetWhiteLabelUsers() =>
        Ok(new { success = true, data = await _userDropdownService.GetWhiteLabelUsersAsync() });

    [HttpGet("ad")]
    public async Task<IActionResult> GetAreaDistributorUsers() =>
        Ok(new { success = true, data = await _userDropdownService.GetAreaDistributorUsersAsync() });

    [HttpGet("md")]
    public async Task<IActionResult> GetMasterDistributorUsers() =>
        Ok(new { success = true, data = await _userDropdownService.GetMasterDistributorUsersAsync() });

    [HttpGet("st")]
    public async Task<IActionResult> GetSalesTeamUsers() =>
        Ok(new { success = true, data = await _userDropdownService.GetSalesTeamUsersAsync() });

    [HttpGet("rt")]
    public async Task<IActionResult> GetRetailerUsers() =>
        Ok(new { success = true, data = await _userDropdownService.GetRetailerUsersAsync() });
}

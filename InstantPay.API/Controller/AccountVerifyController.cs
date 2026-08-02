using InstantPay.Application.Interfaces;
using InstantPay.SharedKernel.RequestPayload;
using InstantPay.SharedKernel.Results.MoneyTransfer.Castler;
using Microsoft.AspNetCore.Mvc;

namespace InstantPay.API.Controller
{
    [Route("api/[controller]")]
    [ApiController]
    public class AccountVerifyController : ControllerBase
    {
        private readonly IAccountVerifyService _accountVerifyService;

        public AccountVerifyController(IAccountVerifyService accountVerifyService)
        {
            _accountVerifyService = accountVerifyService;
        }

        [HttpPost("verify")]
        public async Task<IActionResult> VerifyAccount([FromBody] AccountVerifyRequest request, CancellationToken cancellationToken)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.UserId)
                    || string.IsNullOrWhiteSpace(request.AccountNo)
                    || string.IsNullOrWhiteSpace(request.IfscCode))
                {
                    return Ok(new LoginModel
                    {
                        Status_Code = "0",
                        Message     = "UserId, AccountNo and IfscCode are required",
                        Data        = null
                    });
                }

                var result = await _accountVerifyService.VerifyAccountAsync(request, cancellationToken);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return Ok(new LoginModel
                {
                    Status_Code = "0",
                    Message     = "ERR:500 " + ex.Message,
                    Data        = null
                });
            }
        }
    }
}

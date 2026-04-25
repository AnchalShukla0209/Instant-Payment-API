using InstantPay.Application.Interfaces;
using InstantPay.Application.Interfaces.MoneyTransfer.Castler;
using InstantPay.Application.Services;
using InstantPay.SharedKernel.RequestPayload;
using InstantPay.SharedKernel.RequestPayload.MoneyTransfer.Castler;
using InstantPay.SharedKernel.Results;
using InstantPay.SharedKernel.Results.MoneyTransfer.Castler;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace InstantPay.API.Controller
{
    [Route("api/[controller]")]
    [ApiController]
    public class MoneyTransferController : ControllerBase
    {
        private readonly ICastlerDmtService _dmtService;

        public MoneyTransferController(ICastlerDmtService dmtService)
        {
            _dmtService = dmtService;
        }


        [HttpPost("transfer")]
        public async Task<IActionResult> Transfer([FromBody] DmtRequest model, CancellationToken cancellationToken)
        {
            try
            {
                string ip = HttpContext.Connection.RemoteIpAddress?.ToString()?? "103.49.124.63";

                var result = await _dmtService.MoneyTransfer(model, ip, cancellationToken);

                return Ok(result);
            }
            catch (Exception ex)
            {
                return Ok(new LoginModel
                {
                    Status_Code = "0",
                    Message = "ERR:500 " + ex.Message,
                    Data = null
                });
            }
        }

        [HttpGet("status/{txnId}")]
        public async Task<IActionResult> CheckStatus(string txnId, CancellationToken cancellationToken)
        {
            try
            {
                var result = await _dmtService.CheckStatus(txnId, cancellationToken);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return Ok(new LoginModel
                {
                    Status_Code = "0",
                    Message = "ERR:500 " + ex.Message,
                    Data = null
                });
            }
        }
    }
}

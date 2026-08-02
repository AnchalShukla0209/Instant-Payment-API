using InstantPay.Application.Interfaces.A2Z;
using InstantPay.Application.Interfaces.MoneyTransfer.RechargeKit;
using InstantPay.SharedKernel.RequestPayload.A2Z;
using InstantPay.SharedKernel.RequestPayload.MoneyTransfer.RechargeKit;
using InstantPay.SharedKernel.Results.MoneyTransfer.RechargeKit;
using Microsoft.AspNetCore.Mvc;

namespace InstantPay.API.Controller
{
    [Route("api/[controller]")]
    [ApiController]
    public class CreditCardBillPaymentController : ControllerBase
    {
        private readonly IRechargeKitDmtService _rechargeKitDmtService;
        private readonly IA2ZClient _a2zClient;

        public CreditCardBillPaymentController(IRechargeKitDmtService rechargeKitDmtService, IA2ZClient a2zClient)
        {
            _rechargeKitDmtService = rechargeKitDmtService;
            _a2zClient = a2zClient;
        }

        [HttpGet("operators")]
        public async Task<IActionResult> GetOperators(CancellationToken cancellationToken)
        {
            try
            {
                var result = await _rechargeKitDmtService.GetCreditCardOperatorsAsync(cancellationToken);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return Ok(new RechargeKitOperatorResponse
                {
                    error = 1,
                    msg = "ERR:500 " + ex.Message,
                    status = 3
                });
            }
        }

        [HttpPost("fetch-bill")]
        public async Task<IActionResult> FetchBill(A2ZCreditCardBillFetchRequest request)
        {
            try
            {
                var result = await _a2zClient.FetchCreditCardBillAsync(request);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return Ok(new { status = 0, message = "ERR:500 " + ex.Message, billInfo = (object?)null });
            }
        }

        [HttpPost("pay")]
        public async Task<IActionResult> Pay(CreditCardBillPaymentRequest request, CancellationToken cancellationToken)
        {
            try
            {
                string ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? string.Empty;
                var result = await _rechargeKitDmtService.CreditCardBillPaymentAsync(request, ip, cancellationToken);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return Ok(new { Status_Code = "0", Message = "ERR:500 " + ex.Message, Data = (object?)null });
            }
        }
    }
}

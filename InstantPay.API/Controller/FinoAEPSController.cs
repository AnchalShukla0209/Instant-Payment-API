using InstantPay.Application.Interfaces;
using InstantPay.Application.Interfaces.FinoAeps;
using InstantPay.SharedKernel.RequestPayload.FinoAEPS;
using InstantPay.SharedKernel.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InstantPay.API.Controller
{

    [Route("api/FinoAEPS")]
    [ApiController]
    public class FinoAEPSController : ControllerBase
    {
        private readonly IFinoAepsService _service;
        private readonly IFinoAepsDailyLoginCheckService _dailyLoginCheckService;
        private readonly IFinoMerchantEkycService _merchantEkycService;
        private readonly ILogger<FinoAEPSController> _logger;

        public FinoAEPSController(
            IFinoAepsService service,
            IFinoAepsDailyLoginCheckService dailyLoginCheckService,
            IFinoMerchantEkycService merchantEkycService,
            ILogger<FinoAEPSController> logger)
        {
            _service = service;
            _dailyLoginCheckService = dailyLoginCheckService;
            _merchantEkycService = merchantEkycService;
            _logger = logger;
        }

        /// <summary>
        /// FINO AEPS unified endpoint — handles all txntypes:
        /// be (Balance Enquiry), cw (Cash Withdrawal), ms (Mini Statement),
        /// cd (Cash Deposit), ap (Aadhaar Pay), dl (Daily Login), reg (Registration)
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> GetData(
            [FromBody] FinoAepsRequest request,
            CancellationToken ct)
        {
            if (request == null)
                return Ok(new FinoAepsResponse { Status_Code = "0", Message = "Invalid request body", Data = "" });

            try
            {
                var result = await _service.ProcessAsync(request, ct);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "FinoAEPSController.GetData unhandled error");
                return Ok(new FinoAepsResponse { Status_Code = "0", Message = ex.Message, Data = ex.Message });
            }
        }

        [HttpPost("TransactionStatus")]
        public async Task<IActionResult> TransactionStatus(
            [FromBody] FinoAepsTransactionStatusRequest request,
            CancellationToken ct)
        {
            if (request == null)
                return Ok(new FinoAepsResponse { Status_Code = "0", Message = "Invalid request body", Data = "" });

            try
            {
                var result = await _service.CheckTransactionStatusAsync(request, ct);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "FinoAEPSController.TransactionStatus unhandled error");
                return Ok(new FinoAepsResponse { Status_Code = "0", Message = ex.Message, Data = ex.Message });
            }
        }

        /// <summary>
        /// Check if user has completed FINO AEPS daily login for today
        /// </summary>
        [HttpPost("DailyLoginCheck")]
        public async Task<IActionResult> DailyLoginCheck(
            [FromBody] FinoAepsDailyLoginCheckRequest request,
            CancellationToken ct)
        {
            if (request == null)
                return Ok(new FinoAepsResponse { Status_Code = "0", Message = "Invalid request body", Data = "" });

            try
            {
                var result = await _dailyLoginCheckService.CheckDailyLoginAsync(request, ct);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "FinoAEPSController.DailyLoginCheck unhandled error");
                return Ok(new FinoAepsResponse { Status_Code = "0", Message = ex.Message, Data = ex.Message });
            }
        }

        /// <summary>
        /// FINO Merchant EKYC Registration
        /// </summary>
        [HttpPost("MerchantEkyc")]
        public async Task<IActionResult> MerchantEkyc(
            [FromBody] FinoMerchantEkycRequest request,
            CancellationToken ct)
        {
            if (request == null)
                return Ok(new FinoAepsResponse { Status_Code = "0", Message = "Invalid request body", Data = "" });

            try
            {
                var result = await _merchantEkycService.ProcessAsync(request, ct);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "FinoAEPSController.MerchantEkyc unhandled error");
                return Ok(new FinoAepsResponse { Status_Code = "0", Message = ex.Message, Data = ex.Message });
            }
        }
    }
}

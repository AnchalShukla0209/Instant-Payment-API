using InstantPay.Application.Interfaces;
using InstantPay.Application.Services;
using InstantPay.SharedKernel.RequestPayload;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace InstantPay.API.Controller
{
    [Route("api/[controller]")]
    [ApiController]
    public class IQoreController : ControllerBase
    {
        private readonly IInsuranceInfoService _service;
        private readonly IBillInfoService _bilservice;

        public IQoreController(IInsuranceInfoService service, IBillInfoService bilservice)
        {
            _service = service;
            _bilservice = bilservice;
        }

        [HttpPost("fetch")]
        public async Task<IActionResult> FetchInsurance(
        [FromBody] InsuranceFetchRequestDto request)
        {
            var result = await _service.FetchInsuranceAsync(request);
            return Ok(result);
        }

        [HttpPost("Billfetch")]
        public async Task<IActionResult> FetchBill([FromBody] BillFetchRequestDto request)
        {
            var result = await _bilservice.FetchBillAsync(request);
            return Ok(result);
        }
    }
}

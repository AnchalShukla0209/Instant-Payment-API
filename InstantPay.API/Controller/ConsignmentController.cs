using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Text;

namespace InstantPay.API.Controller
{
    [ApiController]
    [Route("api/consignment")]
    public class ConsignmentController : ControllerBase
    {
        private static readonly HttpClient client = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(40)
        };

        [HttpPost("track")]
        public async Task<IActionResult> Track([FromBody] string code)
        {
            if (string.IsNullOrWhiteSpace(code))
                return BadRequest("Invalid code");

            var request = new HttpRequestMessage(
                HttpMethod.Post,
                "https://lozicsnxtapp.lozics.in/api/Consignmenttracking/GetConsignmentTracking"
            );

            request.Headers.Add("Authorization", "143-0687-190");
            request.Content = new StringContent(
                code,
                Encoding.UTF8,
                "application/json"
            );

            var response = await client.SendAsync(request);
            var content = await response.Content.ReadAsStringAsync();

            return Content(content, "application/json");
        }
    }
}

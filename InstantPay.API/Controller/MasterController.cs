using InstantPay.Application.DTOs;
using InstantPay.Application.Interfaces;
using InstantPay.Infrastructure.Security;
using InstantPay.SharedKernel.Entity;
using InstantPay.SharedKernel.RequestPayload;
using InstantPay.SharedKernel.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace InstantPay.API.Controller
{
    [ApiController]
    [Route("api/[controller]")]
    //[Authorize]
    public class MasterController : ControllerBase
    {
        private readonly IMasterService _masterService;
        private readonly AesEncryptionService _aes;
        public MasterController(IMasterService masterService, AesEncryptionService aes)
        {
            _masterService = masterService;
            _aes = aes;
        }

        [HttpPost("GetSuperAdminDashboardData")]
        public async Task<IActionResult> GetSuperAdminDashboardData(EncryptedRequest request)
        {
            var userId = Request.Headers["userid"].FirstOrDefault();
            var username = Request.Headers["username"].FirstOrDefault();

            if (string.IsNullOrWhiteSpace(userId) || !int.TryParse(userId, out int uid))
            {
                return Unauthorized(new { message = "Invalid or missing userId" });
            }

            if (string.IsNullOrWhiteSpace(username))
            {
                return Unauthorized(new { message = "Invalid or missing username" });
            }
            var decryptedJson = _aes.Decrypt(request.Data);
            var data = JsonSerializer.Deserialize<Superadmindashboardpayload>(decryptedJson);
            var result = await _masterService.GetSuperAdminDashboardData(data.ServiceId, uid, username, (int)data.Year);
            var json = JsonSerializer.Serialize(result);
            var encrypted = _aes.Encrypt(json);
            return Ok(new { data = encrypted });
        }

        [HttpPost("MasterUserDataForDD")]
        public async Task<IActionResult> GetUserMasterUserDataForDD( string Mode="")
        {
            var userId = Request.Headers["userid"].FirstOrDefault();
            var username = Request.Headers["username"].FirstOrDefault();

            if (string.IsNullOrWhiteSpace(userId) || !int.TryParse(userId, out int uid))
            {
                return Unauthorized(new { message = "Invalid or missing userId" });
            }

            if (string.IsNullOrWhiteSpace(username))
            {
                return Unauthorized(new { message = "Invalid or missing username" });
            }
            var result = await _masterService.GetUserMasterDD(Mode);
            return Ok(result);
        }

        [HttpPost("CheckServiceStatus")]
        public async Task<IActionResult> CheckServiceStatus(string Mode = "", int UserId=0)
        {
            var userId = Request.Headers["userid"].FirstOrDefault();
            var username = Request.Headers["username"].FirstOrDefault();

            if (string.IsNullOrWhiteSpace(userId) || !int.TryParse(userId, out int uid))
            {
                return Unauthorized(new { message = "Invalid or missing userId" });
            }

            if (string.IsNullOrWhiteSpace(username))
            {
                return Unauthorized(new { message = "Invalid or missing username" });
            }
            var result = await _masterService.GetServiceStatus(Mode,UserId);
            return Ok(result);
        }

        [HttpPost("RechargePlans")]
        public async Task<IActionResult> RechargePlans([FromBody] PlanRequestPayload objPayload)
        {
            int uid = 0;
            string username = null;

            var userIdClaim = User?.FindFirst("userid");
            var usernameClaim = User?.FindFirst("username");

            if (userIdClaim != null &&
                int.TryParse(userIdClaim.Value, out uid) &&
                usernameClaim != null &&
                !string.IsNullOrWhiteSpace(usernameClaim.Value))
            {
                username = usernameClaim.Value;
            }

            if (uid == 0 || string.IsNullOrWhiteSpace(username))
            {
                var headerUserId = Request.Headers["userid"].FirstOrDefault();
                var headerUsername = Request.Headers["username"].FirstOrDefault();

                if (!string.IsNullOrWhiteSpace(headerUserId) &&
                    int.TryParse(headerUserId, out uid) &&
                    !string.IsNullOrWhiteSpace(headerUsername))
                {
                    username = headerUsername;
                }
            }

            // 3️⃣ Unauthorized ONLY if both sources failed
            if (uid == 0 || string.IsNullOrWhiteSpace(username))
            {
                return Unauthorized(new
                {
                    message = "Invalid or missing userid/username in token and headers"
                });
            }
            var result = await _masterService.GetRechargePlans(objPayload);
            return Ok(result);
        }

        [HttpPost("RechargePlansNew")]
        public async Task<IActionResult> RechargePlansNew([FromBody] PlanRequestPayload objPayload)
        {
            int uid = 0;
            string username = null;

            var userIdClaim = User?.FindFirst("userid");
            var usernameClaim = User?.FindFirst("username");

            if (userIdClaim != null &&
                int.TryParse(userIdClaim.Value, out uid) &&
                usernameClaim != null &&
                !string.IsNullOrWhiteSpace(usernameClaim.Value))
            {
                username = usernameClaim.Value;
            }

            if (uid == 0 || string.IsNullOrWhiteSpace(username))
            {
                var headerUserId = Request.Headers["userid"].FirstOrDefault();
                var headerUsername = Request.Headers["username"].FirstOrDefault();

                if (!string.IsNullOrWhiteSpace(headerUserId) &&
                    int.TryParse(headerUserId, out uid) &&
                    !string.IsNullOrWhiteSpace(headerUsername))
                {
                    username = headerUsername;
                }
            }

            // 3️⃣ Unauthorized ONLY if both sources failed
            if (uid == 0 || string.IsNullOrWhiteSpace(username))
            {
                return Unauthorized(new
                {
                    message = "Invalid or missing userid/username in token and headers"
                });
            }
            var result = await _masterService.GetRechargePlans(objPayload);
            return Ok(result);
        }

        [HttpGet("services")]
        public async Task<IActionResult> GetServices()
        {

            var data = await _masterService.GetServices();
            return Ok(data);
        }

        [HttpGet("{serviceCode}/providers")]
        public async Task<IActionResult> GetProviders(string serviceCode)
        {
            var data = await _masterService.GetProviders(serviceCode);
            return Ok(data);
        }

        [HttpGet("{serviceCode}/features")]
        public async Task<IActionResult> GetFeatures(string serviceCode)
        {
            var data = await _masterService.GetFeatures(serviceCode);
            return Ok(data);
        }

        [HttpGet("{serviceCode}/{providerCode}/features")]
        public async Task<IActionResult> GetProviderFeatures(string serviceCode, string providerCode)
        {
            var features = await _masterService.GetProviderFeatures(serviceCode, providerCode);
            return Ok(features);
        }

        [HttpPost("provider/toggle")]
        public async Task<IActionResult> ToggleProvider([FromBody] ToggleRequestDto req)
        {
            var features = await _masterService.ToggleProvider(req);
            return Ok(features);
        }

        [HttpPost("feature/toggle")]
        public async Task<IActionResult> ToggleFeature([FromBody] ToggleRequestDto req)
        {
            var features = await _masterService.ToggleFeature(req);
            return Ok(features);
        }

        [HttpPost("provider-feature/toggle")]
        public async Task<IActionResult> ToggleProviderFeature([FromBody] ToggleRequestDto req)
        {
            var features = await _masterService.ToggleProviderFeature(req);
            return Ok(features);
        }

        [HttpPost("apiprovider/toggle")]
        public async Task<IActionResult> Toggleapiprovider([FromBody] ToggleRequestDto req)
        {
            var features = await _masterService.ToggleServiceProvider(req);
            return Ok(features);
        }
    }
}

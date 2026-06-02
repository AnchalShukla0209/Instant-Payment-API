using InstantPay.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using System.IO;

namespace InstantPay.API.Controller
{
    [ApiController]
    [Route("api/[controller]")]
    public class AppReleaseController : ControllerBase
    {
        private readonly IAppReleaseService _appReleaseService;
        private readonly ILogger<AppReleaseController> _logger;
        private readonly IWebHostEnvironment _env;

        public AppReleaseController(
            IAppReleaseService appReleaseService,
            ILogger<AppReleaseController> logger,
            IWebHostEnvironment env)
        {
            _appReleaseService = appReleaseService;
            _logger = logger;
            _env = env;
        }

        /// <summary>
        /// Upload a new APK release. Marks all previous releases as inactive.
        /// </summary>
        [HttpPost("upload")]
        public async Task<IActionResult> UploadApk(
            IFormFile file,
            [FromForm] string versionName,
            [FromForm] int versionCode,
            [FromForm] string? releaseNotes = null)
        {
            var userId = Request.Headers["userid"].FirstOrDefault();
            var username = Request.Headers["username"].FirstOrDefault();

            if (string.IsNullOrWhiteSpace(userId) || !int.TryParse(userId, out _))
                return Unauthorized(new { success = false, message = "Invalid or missing userId header." });

            if (string.IsNullOrWhiteSpace(username))
                return Unauthorized(new { success = false, message = "Invalid or missing username header." });

            if (!ModelState.IsValid)
                return BadRequest(new { success = false, message = "Invalid request data.", errors = ModelState });

            var baseUrl = $"{Request.Scheme}://{Request.Host}";

            _logger.LogInformation(
                "APK upload requested by user={UserId} version={VersionName} code={VersionCode}",
                userId, versionName, versionCode);

            var result = await _appReleaseService.UploadApkAsync(file, versionName, versionCode, releaseNotes, baseUrl);

            if (!result.Success)
                return BadRequest(new { success = false, message = result.Message });

            return Ok(new
            {
                success = true,
                message = result.Message,
                data = new
                {
                    versionName = result.VersionName,
                    versionCode = result.VersionCode,
                    downloadUrl = result.DownloadUrl
                }
            });
        }

        /// <summary>
        /// Download an APK file by name (anonymous).
        /// </summary>
        [AllowAnonymous]
        [HttpGet("download/{fileName}")]
        public IActionResult Download(string fileName)
        {
            var safeName = Path.GetFileName(fileName);
            if (string.IsNullOrWhiteSpace(safeName) || !safeName.EndsWith(".apk", StringComparison.OrdinalIgnoreCase))
                return BadRequest(new { success = false, message = "Invalid file name." });

            var filePath = Path.Combine(_env.ContentRootPath, "wwwroot", "UploadFiles", "apk", safeName);

            if (!System.IO.File.Exists(filePath))
            {
                _logger.LogWarning("APK download requested but file not found: {Path}", filePath);
                return NotFound(new { success = false, message = "File not found." });
            }

            return PhysicalFile(filePath, "application/vnd.android.package-archive", safeName);
        }

        /// <summary>
        /// Get all APK releases with optional commonsearch and pagination.
        /// </summary>
        [HttpGet("list")]
        public async Task<IActionResult> GetList(
            [FromQuery] string? commonsearch = null,
            [FromQuery] int pageIndex = 1,
            [FromQuery] int pageSize = 10)
        {
            var userId = Request.Headers["userid"].FirstOrDefault();
            var username = Request.Headers["username"].FirstOrDefault();

            if (string.IsNullOrWhiteSpace(userId) || !int.TryParse(userId, out _))
                return Unauthorized(new { success = false, message = "Invalid or missing userId header." });

            if (string.IsNullOrWhiteSpace(username))
                return Unauthorized(new { success = false, message = "Invalid or missing username header." });

            var baseUrl = $"{Request.Scheme}://{Request.Host}";

            var result = await _appReleaseService.GetAllReleasesAsync(commonsearch, pageIndex, pageSize, baseUrl);

            return Ok(new
            {
                success = true,
                data = result.Records,
                totalCount = result.TotalCount,
                pageIndex = result.PageIndex,
                pageSize = result.PageSize,
                totalPages = result.TotalPages
            });
        }

        /// <summary>
        /// Get the latest active APK version and its download link.
        /// </summary>
        [AllowAnonymous]
        [HttpGet("latest")]
        public async Task<IActionResult> GetLatest()
        {
            var baseUrl = $"{Request.Scheme}://{Request.Host}";

            var release = await _appReleaseService.GetLatestReleaseAsync(baseUrl);

            if (release == null)
                return NotFound(new { success = false, message = "No APK release found." });

            return Ok(new
            {
                success = true,
                data = new
                {
                    versionName = release.VersionName,
                    versionCode = release.VersionCode,
                    downloadUrl = release.DownloadUrl,
                    releaseNotes = release.ReleaseNotes,
                    fileSizeBytes = release.FileSizeBytes,
                    uploadedAt = release.UploadedAt
                }
            });
        }
    }
}

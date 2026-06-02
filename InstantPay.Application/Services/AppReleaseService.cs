using InstantPay.Application.Interfaces;
using InstantPay.Infrastructure.Sql.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace InstantPay.Application.Services
{
    public class AppReleaseService : IAppReleaseService
    {
        private readonly AppDbContext _context;
        private readonly ILogger<AppReleaseService> _logger;
        private readonly string _webRootPath;

        private const string ApkFolder = "UploadFiles/apk";
        private const long MaxFileSizeBytes = 150 * 1024 * 1024; // 150 MB

        public AppReleaseService(AppDbContext context, ILogger<AppReleaseService> logger, string webRootPath)
        {
            _context = context;
            _logger = logger;
            _webRootPath = webRootPath;
        }

        public async Task<AppReleaseUploadResult> UploadApkAsync(
            IFormFile file,
            string versionName,
            int versionCode,
            string? releaseNotes,
            string baseUrl)
        {
            var result = new AppReleaseUploadResult();

            try
            {
                if (file == null || file.Length == 0)
                {
                    result.Success = false;
                    result.Message = "No file provided.";
                    return result;
                }

                if (file.Length > MaxFileSizeBytes)
                {
                    result.Success = false;
                    result.Message = $"File size exceeds the maximum allowed limit of 150 MB.";
                    return result;
                }

                var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
                if (extension != ".apk")
                {
                    result.Success = false;
                    result.Message = "Only .apk files are allowed.";
                    return result;
                }

                if (string.IsNullOrWhiteSpace(versionName))
                {
                    result.Success = false;
                    result.Message = "Version name is required.";
                    return result;
                }

                if (versionCode <= 0)
                {
                    result.Success = false;
                    result.Message = "Version code must be a positive integer.";
                    return result;
                }

                // Ensure the APK folder exists
                var apkFolderPath = Path.Combine(_webRootPath, ApkFolder);
                Directory.CreateDirectory(apkFolderPath);

                // Build a unique file name: app-v{versionName}-{timestamp}.apk
                var safeVersion = versionName.Replace(" ", "_").Replace("/", "_");
                var fileName = $"app-v{safeVersion}-{DateTime.UtcNow:yyyyMMddHHmmss}.apk";
                var fullPath = Path.Combine(apkFolderPath, fileName);

                // Save file to disk
                using (var stream = new FileStream(fullPath, FileMode.Create, FileAccess.Write))
                {
                    await file.CopyToAsync(stream);
                }

                // Deactivate all previous releases
                var previous = await _context.TblAppReleases
                    .Where(r => r.IsActive)
                    .ToListAsync();

                foreach (var prev in previous)
                    prev.IsActive = false;

                // Insert new release record
                var release = new TblAppRelease
                {
                    VersionName = versionName.Trim(),
                    VersionCode = versionCode,
                    FileName = fileName,
                    OriginalFileName = file.FileName,
                    FileSizeBytes = file.Length,
                    ReleaseNotes = releaseNotes?.Trim(),
                    IsActive = true,
                    UploadedAt = DateTime.UtcNow
                };

                _context.TblAppReleases.Add(release);
                await _context.SaveChangesAsync();

                var downloadUrl = BuildDownloadUrl(baseUrl, fileName);

                _logger.LogInformation(
                    "APK uploaded: version={VersionName} code={VersionCode} file={FileName}",
                    versionName, versionCode, fileName);

                result.Success = true;
                result.Message = "APK uploaded successfully.";
                result.VersionName = versionName;
                result.VersionCode = versionCode;
                result.DownloadUrl = downloadUrl;
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading APK");
                result.Success = false;
                result.Message = $"An error occurred while uploading the APK: {ex.Message}";
                return result;
            }
        }

        public async Task<AppReleaseDto?> GetLatestReleaseAsync(string baseUrl)
        {
            try
            {
                var release = await _context.TblAppReleases
                    .Where(r => r.IsActive)
                    .OrderByDescending(r => r.VersionCode)
                    .FirstOrDefaultAsync();

                if (release == null)
                    return null;

                return new AppReleaseDto
                {
                    VersionName = release.VersionName,
                    VersionCode = release.VersionCode,
                    DownloadUrl = BuildDownloadUrl(baseUrl, release.FileName),
                    ReleaseNotes = release.ReleaseNotes,
                    FileSizeBytes = release.FileSizeBytes,
                    UploadedAt = release.UploadedAt
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching latest APK release");
                return null;
            }
        }

        public async Task<AppReleaseListResult> GetAllReleasesAsync(
            string? commonSearch,
            int pageIndex,
            int pageSize,
            string baseUrl)
        {
            try
            {
                if (pageIndex < 1) pageIndex = 1;
                if (pageSize < 1) pageSize = 10;
                if (pageSize > 100) pageSize = 100;

                var query = _context.TblAppReleases.AsNoTracking().AsQueryable();

                if (!string.IsNullOrWhiteSpace(commonSearch))
                {
                    var term = commonSearch.Trim().ToLower();
                    query = query.Where(r =>
                        r.VersionName.ToLower().Contains(term) ||
                        (r.ReleaseNotes != null && r.ReleaseNotes.ToLower().Contains(term)) ||
                        (r.OriginalFileName != null && r.OriginalFileName.ToLower().Contains(term)));
                }

                var totalCount = await query.CountAsync();

                var records = await query
                    .OrderByDescending(r => r.VersionCode)
                    .Skip((pageIndex - 1) * pageSize)
                    .Take(pageSize)
                    .Select(r => new AppReleaseDto
                    {
                        Id = r.Id,
                        VersionName = r.VersionName,
                        VersionCode = r.VersionCode,
                        DownloadUrl = BuildDownloadUrl(baseUrl, r.FileName),
                        ReleaseNotes = r.ReleaseNotes,
                        FileSizeBytes = r.FileSizeBytes,
                        IsActive = r.IsActive,
                        UploadedAt = r.UploadedAt
                    })
                    .ToListAsync();

                return new AppReleaseListResult
                {
                    Records = records,
                    TotalCount = totalCount,
                    PageIndex = pageIndex,
                    PageSize = pageSize
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching APK release list");
                return new AppReleaseListResult();
            }
        }

        private static string BuildDownloadUrl(string baseUrl, string fileName)
        {
            var trimmed = baseUrl.TrimEnd('/');
            return $"{trimmed}/api/AppRelease/download/{Uri.EscapeDataString(fileName)}";
        }
    }
}

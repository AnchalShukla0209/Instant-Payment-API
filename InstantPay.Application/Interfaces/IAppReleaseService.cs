using Microsoft.AspNetCore.Http;
using System;
using System.Threading.Tasks;

namespace InstantPay.Application.Interfaces
{
    public interface IAppReleaseService
    {
        Task<AppReleaseUploadResult> UploadApkAsync(
            IFormFile file,
            string versionName,
            int versionCode,
            string? releaseNotes,
            string baseUrl);

        Task<AppReleaseDto?> GetLatestReleaseAsync(string baseUrl);

        Task<AppReleaseListResult> GetAllReleasesAsync(
            string? commonSearch,
            int pageIndex,
            int pageSize,
            string baseUrl);
    }

    public class AppReleaseUploadResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string? VersionName { get; set; }
        public int? VersionCode { get; set; }
        public string? DownloadUrl { get; set; }
    }

    public class AppReleaseDto
    {
        public int Id { get; set; }
        public string VersionName { get; set; } = string.Empty;
        public int VersionCode { get; set; }
        public string DownloadUrl { get; set; } = string.Empty;
        public string? ReleaseNotes { get; set; }
        public long? FileSizeBytes { get; set; }
        public bool IsActive { get; set; }
        public DateTime UploadedAt { get; set; }
    }

    public class AppReleaseListResult
    {
        public List<AppReleaseDto> Records { get; set; } = new();
        public int TotalCount { get; set; }
        public int PageIndex { get; set; }
        public int PageSize { get; set; }
        public int TotalPages => PageSize > 0 ? (int)Math.Ceiling((double)TotalCount / PageSize) : 0;
    }
}

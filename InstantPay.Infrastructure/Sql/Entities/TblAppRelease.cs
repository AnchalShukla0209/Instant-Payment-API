using System;

namespace InstantPay.Infrastructure.Sql.Entities;

public class TblAppRelease
{
    public int Id { get; set; }
    public string VersionName { get; set; } = null!;
    public int VersionCode { get; set; }
    public string FileName { get; set; } = null!;
    public string? OriginalFileName { get; set; }
    public long? FileSizeBytes { get; set; }
    public string? ReleaseNotes { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime UploadedAt { get; set; }
}

namespace InstantPay.Infrastructure.Sql.Entities;

public sealed class TblUserOnboardingDocument
{
    public long Id { get; set; }
    public int UserId { get; set; }
    public string DocumentType { get; set; } = string.Empty;
    public string CurrentFilePath { get; set; } = string.Empty;
    public string ReviewStatus { get; set; } = "Pending";
    public string? RejectionRemarks { get; set; }
    public int CurrentVersion { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public int? ReviewedBy { get; set; }
    public DateTime? ReviewedAt { get; set; }
}

public sealed class TblUserOnboardingDocumentVersion
{
    public long Id { get; set; }
    public long DocumentId { get; set; }
    public int UserId { get; set; }
    public int VersionNumber { get; set; }
    public string FilePath { get; set; } = string.Empty;
    public string OriginalFileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public string FileHash { get; set; } = string.Empty;
    public string? CorrectionRemarks { get; set; }
    public int UploadedBy { get; set; }
    public DateTime UploadedAt { get; set; }
}

public sealed class TblUserOnboardingReview
{
    public long Id { get; set; }
    public int UserId { get; set; }
    public int SubmissionVersion { get; set; }
    public string ReviewStatus { get; set; } = "Pending";
    public string? FinalRemarks { get; set; }
    public int? ReviewedBy { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}

public sealed class TblUserOnboardingFieldReview
{
    public long Id { get; set; }
    public long ReviewId { get; set; }
    public int UserId { get; set; }
    public string FieldName { get; set; } = string.Empty;
    public string ReviewStatus { get; set; } = "Pending";
    public string? RejectionRemarks { get; set; }
    public int? ReviewedBy { get; set; }
    public DateTime? ReviewedAt { get; set; }
}

public sealed class TblUserOnboardingHistory
{
    public long Id { get; set; }
    public int UserId { get; set; }
    public int OnboardingVersion { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string? FromStatus { get; set; }
    public string ToStatus { get; set; } = string.Empty;
    public string? Remarks { get; set; }
    public int ActorUserId { get; set; }
    public string ActorUserType { get; set; } = string.Empty;
    public string? ChangedFieldsJson { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed class TblUserCredentialDeliveryLog
{
    public long Id { get; set; }
    public int UserId { get; set; }
    public string Channel { get; set; } = "Email";
    public string DestinationMasked { get; set; } = string.Empty;
    public string DeliveryStatus { get; set; } = string.Empty;
    public string IdempotencyKey { get; set; } = string.Empty;
    public string? FailureReason { get; set; }
    public int AttemptCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? SentAt { get; set; }
}

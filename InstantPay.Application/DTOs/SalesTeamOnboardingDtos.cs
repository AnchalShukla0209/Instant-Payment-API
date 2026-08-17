using System.ComponentModel.DataAnnotations;

namespace InstantPay.Application.DTOs;

public sealed class SaveOnboardingDraftRequest
{
    public int UserId { get; set; }
    public string? RowVersion { get; set; }
    public string? UserType { get; set; }
    public string? CompanyName { get; set; }
    public string? Name { get; set; }
    public string? FatherName { get; set; }
    public string? Username { get; set; }
    public string? EmailId { get; set; }
    public string? Phone { get; set; }
    public string? PanCard { get; set; }
    public string? AadharCard { get; set; }
    public string? AddressLine1 { get; set; }
    public string? AddressLine2 { get; set; }
    public string? State { get; set; }
    public string? City { get; set; }
    public string? Pincode { get; set; }
    public string? ShopAddress { get; set; }
    public string? ShopState { get; set; }
    public string? ShopCity { get; set; }
    public string? ShopZipCode { get; set; }
    public string? Latitude { get; set; }
    public string? Longitude { get; set; }
    public string? WLId { get; set; }
    public string? ADId { get; set; }
    public string? MDId { get; set; }
    public int? CommissionPlanId { get; set; }
}

public sealed record OnboardingDraftResponse(int UserId, string OnboardingStatus, int Version, string RowVersion);

public class OnboardingListQuery
{
    [Range(1, int.MaxValue)] public int PageIndex { get; set; } = 1;
    [Range(1, 100)] public int PageSize { get; set; } = 20;
    [StringLength(100)] public string? Search { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    [StringLength(30)] public string? Status { get; set; }
}

public sealed record OnboardingListItem(
    int UserId, string Name, string Username, string Phone, string EmailId,
    string UserType, string OnboardingStatus, DateTime? CreatedAt, DateTime? UpdatedAt);

public sealed record OnboardingPagedResponse(
    IReadOnlyList<OnboardingListItem> Data, int TotalCount, int PageIndex, int PageSize);

public sealed record OnboardingCommandResult(bool Success, string Message, int UserId, string Status);
public sealed record OnboardingDocumentResponse(long DocumentId, string DocumentType, int Version, string ReviewStatus);
public sealed record SubmitOnboardingRequest(string? RowVersion);
public sealed record OwnedOnboardingDetail(
    int UserId, string? UserType, string? CompanyName, string? Name, string? FatherName,
    string? Username, string? EmailId, string? Phone, string? PanCard, string AadhaarMasked,
    string? AddressLine1, string? AddressLine2, string? State, string? City, string? Pincode,
    string? ShopAddress, string? ShopState, string? ShopCity, string? ShopZipCode,
    string? Latitude, string? Longitude, string? WLId, string? ADId, string? MDId,
    int? CommissionPlanId, bool IsEmailVerified, bool IsPhoneVerified, bool IsPanVerified, bool IsAadhaarVerified,
    string OnboardingStatus, int Version, string RowVersion,
    string? FinalReviewRemarks, IReadOnlyList<OwnedOnboardingDocument> Documents, IReadOnlyList<OnboardingHistoryItem> History);
public sealed record OwnedOnboardingDocument(long Id, string DocumentType, int Version, string ReviewStatus, string? RejectionRemarks, string? CorrectionRemarks, IReadOnlyList<OnboardingDocumentVersionItem> Versions);
public sealed record OnboardingDocumentVersionItem(long Id, int VersionNumber, string OriginalFileName, long FileSize, string? CorrectionRemarks, DateTime UploadedAt);
public sealed record OnboardingHistoryItem(long Id, int OnboardingVersion, string EventType, string? FromStatus, string ToStatus, string? Remarks, int ActorUserId, string ActorUserType, string ActorName, string? IpAddress, DateTime CreatedAt);

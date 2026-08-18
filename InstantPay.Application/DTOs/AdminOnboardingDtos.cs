using System.ComponentModel.DataAnnotations;

namespace InstantPay.Application.DTOs;

public sealed class ReviewDecisionRequest
{
    public ReviewDecisionRequest() { }
    public ReviewDecisionRequest(string status, string? remarks) => (Status, Remarks) = (status, remarks);

    [Required]
    public string Status { get; init; } = string.Empty;

    [StringLength(1000)]
    public string? Remarks { get; init; }
}

public sealed class FinalRejectionRequest
{
    public FinalRejectionRequest() { }
    public FinalRejectionRequest(string remarks) => Remarks = remarks;

    [Required, StringLength(2000, MinimumLength = 5)]
    public string Remarks { get; init; } = string.Empty;
}

public sealed record AdminOnboardingListItem(
    int UserId, string Name, string Username, string Phone, string EmailId, string UserType,
    string PanCard, string AadhaarMasked, string OnboardingStatus, int SubmissionVersion, int SalesTeamId, string SalesPersonName,
    DateTime? SubmittedAt);

public sealed record AdminOnboardingPagedResponse(
    IReadOnlyList<AdminOnboardingListItem> Data, int TotalCount, int PageIndex, int PageSize);

public sealed class AdminOnboardingListQuery : OnboardingListQuery
{
    public int? SalesTeamId { get; set; }
}

public sealed record AdminOnboardingReviewDetail(
    int UserId, object User, object Review, IReadOnlyList<object> Fields,
    IReadOnlyList<object> Documents, IReadOnlyList<object> History, string RowVersion);

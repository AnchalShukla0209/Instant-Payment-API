using System.ComponentModel.DataAnnotations;

namespace InstantPay.Application.DTOs;

public sealed record ReviewDecisionRequest(
    [property: Required] string Status,
    [property: StringLength(1000)] string? Remarks);

public sealed record FinalRejectionRequest(
    [property: Required, StringLength(2000, MinimumLength = 5)] string Remarks);

public sealed record AdminOnboardingListItem(
    int UserId, string Name, string Username, string Phone, string EmailId, string UserType,
    string OnboardingStatus, int SubmissionVersion, int SalesTeamId, string SalesPersonName,
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

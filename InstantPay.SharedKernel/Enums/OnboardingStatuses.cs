namespace InstantPay.SharedKernel.Enums;

public static class OnboardingStatuses
{
    public const string Draft = "Draft";
    public const string PendingReview = "PendingReview";
    public const string Rejected = "Rejected";
    public const string PendingReReview = "PendingReReview";
    public const string Approved = "Approved";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        Draft, PendingReview, Rejected, PendingReReview, Approved
    };
}

public static class OnboardingReviewStatuses
{
    public const string Pending = "Pending";
    public const string Approved = "Approved";
    public const string Rejected = "Rejected";
}

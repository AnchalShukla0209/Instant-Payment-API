using System.ComponentModel.DataAnnotations;
using InstantPay.Application.DTOs;
using InstantPay.SharedKernel.Enums;
using Xunit;

namespace InstantPay.Onboarding.Tests;

public sealed class OnboardingContractTests
{
    [Theory]
    [InlineData("Draft")]
    [InlineData("PendingReview")]
    [InlineData("Rejected")]
    [InlineData("PendingReReview")]
    [InlineData("Approved")]
    public void Workflow_statuses_are_registered(string status) => Assert.Contains(status, OnboardingStatuses.All);

    [Fact]
    public void Unknown_workflow_status_is_not_registered() => Assert.DoesNotContain("Active", OnboardingStatuses.All);

    [Theory]
    [InlineData("")]
    [InlineData("no")]
    [InlineData("    ")]
    public void Final_rejection_requires_meaningful_remarks(string remarks)
    {
        var results = Validate(new FinalRejectionRequest(remarks));
        Assert.NotEmpty(results);
    }

    [Fact]
    public void Final_rejection_accepts_valid_remarks() => Assert.Empty(Validate(new FinalRejectionRequest("Aadhaar image is unreadable.")));

    [Theory]
    [InlineData(0, 20)]
    [InlineData(1, 0)]
    [InlineData(1, 101)]
    public void Paging_contract_rejects_out_of_range_values(int pageIndex, int pageSize)
    {
        Assert.NotEmpty(Validate(new OnboardingListQuery { PageIndex = pageIndex, PageSize = pageSize }));
    }

    [Fact]
    public void Rejection_decision_limits_remarks_length()
    {
        Assert.NotEmpty(Validate(new ReviewDecisionRequest(OnboardingReviewStatuses.Rejected, new string('x', 1001))));
    }

    private static List<ValidationResult> Validate(object value)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(value, new ValidationContext(value), results, true);
        return results;
    }
}

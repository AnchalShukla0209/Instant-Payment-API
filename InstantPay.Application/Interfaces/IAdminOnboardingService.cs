using InstantPay.Application.DTOs;

namespace InstantPay.Application.Interfaces;

public interface IAdminOnboardingService
{
    Task<AdminOnboardingPagedResponse> GetListAsync(AdminOnboardingListQuery query, CancellationToken cancellationToken);
    Task<AdminOnboardingReviewDetail> GetDetailAsync(int userId, CancellationToken cancellationToken);
    Task<OnboardingCommandResult> ReviewFieldAsync(int userId, long fieldReviewId, ReviewDecisionRequest request, int adminId, string? ipAddress, string? userAgent, CancellationToken cancellationToken);
    Task<OnboardingCommandResult> ReviewDocumentAsync(int userId, long documentId, ReviewDecisionRequest request, int adminId, string? ipAddress, string? userAgent, CancellationToken cancellationToken);
    Task<OnboardingCommandResult> RejectAsync(int userId, FinalRejectionRequest request, int adminId, string? ipAddress, string? userAgent, CancellationToken cancellationToken);
    Task<OnboardingCommandResult> ApproveAsync(int userId, string? rowVersion, int adminId, string? ipAddress, string? userAgent, CancellationToken cancellationToken);
    Task<OnboardingCommandResult> RetryCredentialEmailAsync(int userId, int adminId, string? ipAddress, string? userAgent, CancellationToken cancellationToken);
}

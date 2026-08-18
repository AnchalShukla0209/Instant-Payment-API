using InstantPay.Application.DTOs;
using InstantPay.Infrastructure.Sql.Entities;
using Microsoft.AspNetCore.Http;

namespace InstantPay.Application.Interfaces;

public interface ISalesTeamOnboardingService
{
    Task<OnboardingDraftResponse> SaveDraftAsync(SaveOnboardingDraftRequest request, int salesTeamId, string? ipAddress, string? userAgent, CancellationToken cancellationToken);
    Task<OwnedOnboardingDetail?> FindOwnedDraftByPhoneAsync(string phone, int salesTeamId, CancellationToken cancellationToken);
    Task<OwnedOnboardingDetail> GetOwnedDetailAsync(int userId, int salesTeamId, CancellationToken cancellationToken);
    Task<OnboardingPagedResponse> GetOwnedListAsync(OnboardingListQuery query, int salesTeamId, CancellationToken cancellationToken);
    Task<IdentityAvailabilityResponse> CheckIdentityAvailabilityAsync(IdentityAvailabilityRequest request, int salesTeamId, CancellationToken cancellationToken);
    Task<OnboardingCommandResult> SubmitAsync(int userId, string? rowVersion, int salesTeamId, string? ipAddress, string? userAgent, CancellationToken cancellationToken);
    Task<OnboardingDocumentResponse> UploadDocumentAsync(int userId, string documentType, IFormFile file, string? correctionRemarks, int salesTeamId, CancellationToken cancellationToken);
}

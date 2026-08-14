using InstantPay.Application.DTOs;

namespace InstantPay.Application.Interfaces;

public interface IPartnerDashboardService
{
    Task<PartnerDashboardResponse> GetDashboardAsync(
        int partnerId,
        string userType,
        int days,
        CancellationToken cancellationToken);

    Task<PartnerWalletResponse> GetWalletAsync(
        int partnerId,
        CancellationToken cancellationToken);
}

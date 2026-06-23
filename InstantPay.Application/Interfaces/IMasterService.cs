using InstantPay.Application.DTOs;
using InstantPay.SharedKernel.Entity;
using InstantPay.SharedKernel.RequestPayload;
using InstantPay.SharedKernel.Results;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InstantPay.Application.Interfaces
{
    public interface IMasterService
    {
        Task<ServiceMasterDTO> GetSuperAdminDashboardData(int? ServiceId, int userId, string username, int year);
        Task<IReadOnlyList<UserMasterDataForDD>> GetUserMasterDD(string Mode);

        Task<ServiceStatusResponse> GetServiceStatus(string Mode = "", int UserId = 0);

        Task<string> GetRechargePlans(PlanRequestPayload payload);
        Task<string> GetRechargePlansNew(PlanRequestPayload payload);

        Task<List<ServiceDTO>> GetServices();

        Task<List<ProviderDTO>> GetProviders(string serviceCode);

        Task<List<FeatureDto>> GetFeatures(string serviceCode);

        Task<List<FeatureDto>> GetProviderFeatures(string serviceCode, string providerCode);

        Task<ResponseSuccess> ToggleProvider(ToggleRequestDto req);
        Task<ResponseSuccess> ToggleFeature(ToggleRequestDto req);
        Task<ResponseSuccess> ToggleProviderFeature(ToggleRequestDto req);

        Task<ResponseSuccess> ToggleServiceProvider(ToggleRequestDto req);
    }
}

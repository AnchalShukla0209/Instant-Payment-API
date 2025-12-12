using InstantPay.Application.DTOs;
using InstantPay.SharedKernel.Entity;
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
    }
}

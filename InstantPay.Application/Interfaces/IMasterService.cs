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
    }
}

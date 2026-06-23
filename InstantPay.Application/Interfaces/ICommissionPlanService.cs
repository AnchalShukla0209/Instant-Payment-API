using InstantPay.Application.DTOs;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace InstantPay.Application.Interfaces
{
    public interface ICommissionPlanService
    {
        Task<CommissionPlanDto> CreateCommissionPlan(CreateCommissionPlanDto dto);
        Task<CommissionPlanDto> UpdateCommissionPlan(UpdateCommissionPlanDto dto);
        Task<CommissionPlanDto?> GetCommissionPlanById(int id);
        Task<List<CommissionPlanDropdownDto>> GetCommissionPlansForDropdown();
        Task<(List<CommissionPlanDto> items, int totalCount)> GetCommissionPlansWithPagination(int pageNumber, int pageSize, string? search = null);
        Task<bool> DeleteCommissionPlan(int id);
    }
}

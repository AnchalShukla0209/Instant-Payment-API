using InstantPay.Application.DTOs;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace InstantPay.Application.Interfaces
{
    public interface IPlanDetailService
    {
        Task<PlanDetailDto> CreatePlanDetail(CreatePlanDetailDto dto);
        Task<PlanDetailDto> UpdatePlanDetail(UpdatePlanDetailDto dto);
        Task<PlanDetailDto?> GetPlanDetailById(int id);
        Task<List<PlanDetailDropdownDto>> GetPlanDetailsForDropdown();
        Task<(List<PlanDetailDto> items, int totalCount)> GetPlanDetailsWithPagination(int pageNumber, int pageSize, string? search = null);
        Task<bool> DeletePlanDetail(int id);
    }
}

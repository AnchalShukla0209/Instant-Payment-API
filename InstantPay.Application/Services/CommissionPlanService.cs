using InstantPay.Application.DTOs;
using InstantPay.Application.Interfaces;
using InstantPay.Infrastructure.Sql.Entities;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace InstantPay.Application.Services
{
    public class CommissionPlanService : ICommissionPlanService
    {
        private readonly AppDbContext _context;

        public CommissionPlanService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<CommissionPlanDto> CreateCommissionPlan(CreateCommissionPlanDto dto)
        {
            var existingCommissionPlan = await _context.CommissionPlans
                .FirstOrDefaultAsync(cp => cp.SlabRange.ToLower() == dto.SlabRange.ToLower() &&
                                          cp.ServiceId == dto.ServiceId &&
                                          cp.OperatorId == dto.OperatorId &&
                                          cp.PlanId == dto.PlanId &&
                                          (cp.APICode == dto.APICode || (cp.APICode == null && dto.APICode == null)));
            
            if (existingCommissionPlan != null)
                throw new ArgumentException("Commission plan with this Slab, ServiceId, OperatorId, and APICode and Plan Name already exists");

            var entity = new CommissionPlan
            {
                PlanId = dto.PlanId,
                SlabRange = dto.SlabRange,
                AdminShare = dto.AdminShare,
                WlAdminShare = dto.WlAdminShare,
                MdShare = dto.MdShare,
                AdShare = dto.AdShare,
                RtShare = dto.RtShare,
                CommissionType = dto.CommissionType,
                ServiceId = dto.ServiceId,
                APICode = dto.APICode,
                OperatorId = dto.OperatorId,
                CreatedBy = dto.CreatedBy,
                CreatedAt = DateTime.UtcNow
            };

            _context.CommissionPlans.Add(entity);
            await _context.SaveChangesAsync();

            var plan = await _context.PlanDetails.FindAsync(dto.PlanId);

            return new CommissionPlanDto
            {
                Id = entity.Id,
                PlanId = entity.PlanId,
                SlabRange = entity.SlabRange,
                AdminShare = entity.AdminShare,
                WlAdminShare = entity.WlAdminShare,
                MdShare = entity.MdShare,
                AdShare = entity.AdShare,
                RtShare = entity.RtShare,
                CommissionType = entity.CommissionType,
                ServiceId = entity.ServiceId,
                APICode = entity.APICode,
                OperatorId = entity.OperatorId,
                CreatedAt = entity.CreatedAt,
                CreatedBy = entity.CreatedBy,
                PlanName = plan?.PlanName
            };
        }

        public async Task<CommissionPlanDto> UpdateCommissionPlan(UpdateCommissionPlanDto dto)
        {
            var entity = await _context.CommissionPlans.FindAsync(dto.Id);
            if (entity == null)
                throw new ArgumentException("CommissionPlan not found");

            var existingCommissionPlan = await _context.CommissionPlans
                .FirstOrDefaultAsync(cp => cp.SlabRange.ToLower() == dto.SlabRange.ToLower() &&
                                          cp.ServiceId == dto.ServiceId &&
                                          cp.OperatorId == dto.OperatorId &&
                                          cp.PlanId == dto.PlanId &&
                                          (cp.APICode == dto.APICode || (cp.APICode == null && dto.APICode == null)) &&
                                          cp.Id != dto.Id);
            
            if (existingCommissionPlan != null)
                throw new ArgumentException("Commission plan with this Slab, ServiceId, OperatorId, and APICode and PlanName already exists");

            entity.PlanId = dto.PlanId;
            entity.SlabRange = dto.SlabRange;
            entity.AdminShare = dto.AdminShare;
            entity.WlAdminShare = dto.WlAdminShare;
            entity.MdShare = dto.MdShare;
            entity.AdShare = dto.AdShare;
            entity.RtShare = dto.RtShare;
            entity.CommissionType = dto.CommissionType;
            entity.ServiceId = dto.ServiceId;
            entity.APICode = dto.APICode;
            entity.OperatorId = dto.OperatorId;

            _context.CommissionPlans.Update(entity);
            await _context.SaveChangesAsync();

            var plan = await _context.PlanDetails.FindAsync(dto.PlanId);

            return new CommissionPlanDto
            {
                Id = entity.Id,
                PlanId = entity.PlanId,
                SlabRange = entity.SlabRange,
                AdminShare = entity.AdminShare,
                WlAdminShare = entity.WlAdminShare,
                MdShare = entity.MdShare,
                AdShare = entity.AdShare,
                RtShare = entity.RtShare,
                CommissionType = entity.CommissionType,
                ServiceId = entity.ServiceId,
                APICode = entity.APICode,
                OperatorId = entity.OperatorId,
                CreatedAt = entity.CreatedAt,
                CreatedBy = entity.CreatedBy,
                PlanName = plan?.PlanName
            };
        }

        public async Task<CommissionPlanDto?> GetCommissionPlanById(int id)
        {
            var query = from cp in _context.CommissionPlans
                        join pd in _context.PlanDetails on cp.PlanId equals pd.Id into pdJoin
                        from pd in pdJoin.DefaultIfEmpty()
                        where cp.Id == id
                        select new CommissionPlanDto
                        {
                            Id = cp.Id,
                            PlanId = cp.PlanId,
                            SlabRange = cp.SlabRange,
                            AdminShare = cp.AdminShare,
                            WlAdminShare = cp.WlAdminShare,
                            MdShare = cp.MdShare,
                            AdShare = cp.AdShare,
                            RtShare = cp.RtShare,
                            CommissionType = cp.CommissionType,
                            ServiceId = cp.ServiceId,
                            APICode = cp.APICode,
                            OperatorId = cp.OperatorId,
                            CreatedAt = cp.CreatedAt,
                            CreatedBy = cp.CreatedBy,
                            PlanName = pd != null ? pd.PlanName : null
                        };

            return await query.FirstOrDefaultAsync();
        }

        public async Task<List<CommissionPlanDropdownDto>> GetCommissionPlansForDropdown()
        {
            var query = from cp in _context.CommissionPlans
                        join pd in _context.PlanDetails on cp.PlanId equals pd.Id into pdJoin
                        from pd in pdJoin.DefaultIfEmpty()
                        select new CommissionPlanDropdownDto
                        {
                            Id = cp.Id,
                            SlabRange = cp.SlabRange,
                            PlanName = pd != null ? pd.PlanName : "Unknown"
                        };

            return await query.OrderBy(x => x.PlanName).ThenBy(x => x.SlabRange).ToListAsync();
        }

        public async Task<(List<CommissionPlanDto> items, int totalCount)> GetCommissionPlansWithPagination(int pageNumber, int pageSize, string? search = null)
        {
            var query = from cp in _context.CommissionPlans
                        join pd in _context.PlanDetails on cp.PlanId equals pd.Id into pdJoin
                        from pd in pdJoin.DefaultIfEmpty()
                        join svc in _context.Tbl_Services on cp.ServiceId equals svc.Id into svcJoin
                        from svc in svcJoin.DefaultIfEmpty()
                        join ac in _context.APICodes on cp.APICode equals ac.APICodeValue into acJoin
                        from ac in acJoin.DefaultIfEmpty()
                        select new CommissionPlanDto
                        {
                            Id = cp.Id,
                            PlanId = cp.PlanId,
                            SlabRange = cp.SlabRange,
                            AdminShare = cp.AdminShare,
                            WlAdminShare = cp.WlAdminShare,
                            MdShare = cp.MdShare,
                            AdShare = cp.AdShare,
                            RtShare = cp.RtShare,
                            CommissionType = cp.CommissionType,
                            ServiceId = cp.ServiceId,
                            APICode = cp.APICode,
                            OperatorId = cp.OperatorId,
                            CreatedAt = cp.CreatedAt,
                            CreatedBy = cp.CreatedBy,
                            PlanName = pd != null ? pd.PlanName : null,
                            ServiceName = svc != null ? svc.ServiceName : null,
                            APIName = ac != null ? ac.Name : null
                        };

            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(cp =>
                    cp.SlabRange.Contains(search) ||
                    (cp.PlanName != null && cp.PlanName.Contains(search)) ||
                    (cp.ServiceName != null && cp.ServiceName.Contains(search)) ||
                    (cp.APICode != null && cp.APICode.Contains(search)) ||
                    (cp.APIName != null && cp.APIName.Contains(search)) ||
                    cp.CommissionType.Contains(search));
            }

            var totalCount = await query.CountAsync();

            var items = await query
                .OrderBy(cp => cp.PlanId)
                .ThenBy(cp => cp.SlabRange)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (items, totalCount);
        }

        public async Task<bool> DeleteCommissionPlan(int id)
        {
            var entity = await _context.CommissionPlans.FindAsync(id);
            if (entity == null)
                return false;

            _context.CommissionPlans.Remove(entity);
            await _context.SaveChangesAsync();
            return true;
        }
    }
}

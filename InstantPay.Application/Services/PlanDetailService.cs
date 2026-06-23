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
    public class PlanDetailService : IPlanDetailService
    {
        private readonly AppDbContext _context;

        public PlanDetailService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<PlanDetailDto> CreatePlanDetail(CreatePlanDetailDto dto)
        {
            var existingPlan = await _context.PlanDetails
                .FirstOrDefaultAsync(p => p.PlanName.ToLower() == dto.PlanName.ToLower());
            
            if (existingPlan != null)
                throw new ArgumentException("Plan with this name already exists");

            var entity = new PlanDetail
            {
                PlanName = dto.PlanName,
                IsActive = dto.IsActive,
                CreatedBy = dto.CreatedBy,
                CreatedAt = DateTime.UtcNow
            };

            _context.PlanDetails.Add(entity);
            await _context.SaveChangesAsync();

            return new PlanDetailDto
            {
                Id = entity.Id,
                PlanName = entity.PlanName,
                IsActive = entity.IsActive,
                CreatedBy = entity.CreatedBy,
                CreatedAt = entity.CreatedAt
            };
        }

        public async Task<PlanDetailDto> UpdatePlanDetail(UpdatePlanDetailDto dto)
        {
            var entity = await _context.PlanDetails.FindAsync(dto.Id);
            if (entity == null)
                throw new ArgumentException("PlanDetail not found");

            var existingPlan = await _context.PlanDetails
                .FirstOrDefaultAsync(p => p.PlanName.ToLower() == dto.PlanName.ToLower() && p.Id != dto.Id);
            
            if (existingPlan != null)
                throw new ArgumentException("Plan with this name already exists");

            entity.PlanName = dto.PlanName;
            entity.IsActive = dto.IsActive;

            _context.PlanDetails.Update(entity);
            await _context.SaveChangesAsync();

            return new PlanDetailDto
            {
                Id = entity.Id,
                PlanName = entity.PlanName,
                IsActive = entity.IsActive,
                CreatedBy = entity.CreatedBy,
                CreatedAt = entity.CreatedAt
            };
        }

        public async Task<PlanDetailDto?> GetPlanDetailById(int id)
        {
            var entity = await _context.PlanDetails.FindAsync(id);
            if (entity == null)
                return null;

            return new PlanDetailDto
            {
                Id = entity.Id,
                PlanName = entity.PlanName,
                IsActive = entity.IsActive,
                CreatedBy = entity.CreatedBy,
                CreatedAt = entity.CreatedAt
            };
        }

        public async Task<List<PlanDetailDropdownDto>> GetPlanDetailsForDropdown()
        {
            return await _context.PlanDetails
                .Where(p => p.IsActive)
                .Select(p => new PlanDetailDropdownDto
                {
                    Id = p.Id,
                    PlanName = p.PlanName
                })
                .OrderBy(p => p.PlanName)
                .ToListAsync();
        }

        public async Task<(List<PlanDetailDto> items, int totalCount)> GetPlanDetailsWithPagination(int pageNumber, int pageSize, string? search = null)
        {
            var query = _context.PlanDetails.AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(p => p.PlanName.Contains(search));
            }

            var totalCount = await query.CountAsync();

            var items = await query
                .OrderBy(p => p.Id)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .Select(p => new PlanDetailDto
                {
                    Id = p.Id,
                    PlanName = p.PlanName,
                    IsActive = p.IsActive,
                    CreatedBy = p.CreatedBy,
                    CreatedAt = p.CreatedAt
                })
                .ToListAsync();

            return (items, totalCount);
        }

        public async Task<bool> DeletePlanDetail(int id)
        {
            var entity = await _context.PlanDetails.FindAsync(id);
            if (entity == null)
                return false;

            _context.PlanDetails.Remove(entity);
            await _context.SaveChangesAsync();
            return true;
        }
    }
}

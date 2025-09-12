using InstantPay.Application.Interfaces;
using InstantPay.Infrastructure.Sql.Entities;
using InstantPay.SharedKernel.Entity;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InstantPay.Application.Services
{
    public class ServicesService : IServiceService
    {

        private readonly AppDbContext _context;

        public ServicesService(AppDbContext context)
        {
            _context = context;
        }


        public async Task<PageResult<Tbl_Services>> GetAllAsync(int pageIndex = 1, int pageSize = 10)
        {
            var query = _context.Tbl_Services
                .Where(s => s.IsDeleted == false)
                .OrderBy(s => s.ServiceName)
                .AsQueryable();

            var totalCount = await query.CountAsync();

            var items = await query
                .Skip((pageIndex - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return new PageResult<Tbl_Services>
            {
                Items = items,
                TotalCount = totalCount
            };
        }


        public async Task<Tbl_Services> GetByIdAsync(int id)
        {
            return await _context.Tbl_Services
                .FirstOrDefaultAsync(s => s.Id == id && s.IsDeleted == false);
        }

        // Insert new
        public async Task<string> CreateAsync(ServiceDtoRequest dto)
        {
            if (string.IsNullOrWhiteSpace(dto.ServiceName))
                return "Service name is required.";

            var service = new Tbl_Services
            {
                ServiceName = dto.ServiceName.Trim(),
                ServicePath = dto.ServicePath,
                IsActive = dto.IsActive,
                IsDeleted = false
            };

            _context.Tbl_Services.Add(service);
            await _context.SaveChangesAsync();
            return null; // success
        }

        // Update existing
        public async Task<string> UpdateAsync(int id, ServiceDtoRequest dto)
        {
            var service = await _context.Tbl_Services.FindAsync(id);
            if (service == null || service.IsDeleted == true)
            {
                return "Service not found.";
            }

            if (string.IsNullOrWhiteSpace(dto.ServiceName))
            {
                return "Service name is required.";
            }

            service.ServiceName = dto.ServiceName.Trim();
            service.ServicePath = dto.ServicePath;
            service.IsActive = dto.IsActive;

            _context.Tbl_Services.Update(service);
            await _context.SaveChangesAsync();
            return null;
        }

        // Soft Delete
        public async Task<string> DeleteAsync(int id)
        {
            var service = await _context.Tbl_Services.FindAsync(id);
            if (service == null || service.IsDeleted == true)
            {
                return "Service not found.";
            }

            service.IsDeleted = true;
            _context.Tbl_Services.Update(service);
            await _context.SaveChangesAsync();
            return null;
        }

    }
}

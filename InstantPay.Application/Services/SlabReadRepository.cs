using InstantPay.Application.Interfaces;
using InstantPay.Infrastructure.Security;
using InstantPay.Infrastructure.Sql.Entities;
using InstantPay.SharedKernel.Entity;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using MongoDB.Bson.IO;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InstantPay.Application.Services
{
    public class SlabReadRepository: ISlabReadRepository
    {
        private readonly AppDbContext _context;
        public SlabReadRepository(AppDbContext context) {

            _context = context;
        }

        public async Task<PagedResult<SlabInfoDto>> GetSlabInfoAsync(string? serviceName, int pageIndex, int pageSize)
        {
            // Ensure valid paging parameters
            if (pageIndex <= 0) pageIndex = 1;
            if (pageSize <= 0) pageSize = 50;

            int skip = (pageIndex - 1) * pageSize;

            // Base query
            var baseQuery = _context.TblSlabNames.AsNoTracking();

            if (!string.IsNullOrWhiteSpace(serviceName) &&
                !serviceName.Equals("ALL", StringComparison.OrdinalIgnoreCase))
            {
                baseQuery = baseQuery.Where(x => x.ServiceName == serviceName);
            }

            // First query: total count
            var totalCount = await baseQuery.CountAsync();

            // Second query: paged items
            var items = await baseQuery
                .OrderBy(x => x.Id)
                .Skip(skip)
                .Take(pageSize)
                .Select(e => new SlabInfoDto
                {
                    Id = e.Id,
                    ServiceName = e.ServiceName ?? string.Empty,
                    SlabName = e.SlabName ?? string.Empty,
                    IPShare = (decimal)e.Ipshare,
                    WLShare = (decimal)e.Wlshare,
                    CommissionType = e.CommissionType ?? string.Empty
                })
                .ToListAsync();

            // Return paged result
            return new PagedResult<SlabInfoDto>(items, totalCount, pageIndex, pageSize);
        }


        public async Task<UpdateCommissionResult> Handle(UpdateCommissionCommand request)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            {
                try
                {
                    // Update tblSlabName
                    var slab = await _context.TblSlabNames
                        .FirstOrDefaultAsync(x => x.Id == request.Id);

                    if (slab == null)
                        return new UpdateCommissionResult
                        {
                            ErrorMsg = "Slab not found",
                            Flag = false
                        };

                    slab.Ipshare = request.IPShare;
                    slab.Wlshare = request.WLShare;
                    slab.CommissionType = request.CommissionType;

                    // Update tblcommissionslab
                    var commissionSlabs = await _context.Tblcommissionslabs
                        .Where(x => x.SlabId == Convert.ToString(request.Id))
                        .ToListAsync();

                    foreach (var c in commissionSlabs)
                    {
                        c.Ipshare = request.IPShare;
                        c.WlShare = request.WLShare;
                        c.CommissionType = request.CommissionType;
                    }

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    return new UpdateCommissionResult
                    {
                        ErrorMsg = "Commission Updated Successfully",
                        Flag = true
                    };
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    return new UpdateCommissionResult
                    {
                        ErrorMsg = "Error in Transaction: " + ex.Message,
                        Flag = false
                    };
                }
            }
        }
    }
}

using InstantPay.Application.Interfaces;
using InstantPay.SharedKernel.Entity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using InstantPay.Application.Interfaces;
using InstantPay.Infrastructure.Sql.Entities;
using Microsoft.EntityFrameworkCore;

namespace InstantPay.Application.Services
{
    public class OperatorReadRepository : IOperatorReadRepository
    {
        private readonly AppDbContext _context;

        public OperatorReadRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<List<OperatorDto>> GetByServiceNameAsync(string serviceName)
        {
            return await _context.Tbloperators
             .Where(o => EF.Functions.Like(o.ServiceName, $"%{serviceName}%") && o.Status == "Active")
             .Select(o => new OperatorDto
             {
                 Id = o.Id,
                 ServiceName = o.ServiceName,
                 OperatorName = o.OperatorName,
                 Spkey = o.Spkey,
                 Picture = o.Picture,
                 Status = o.Status
             })
             .ToListAsync();

        }
    }

}

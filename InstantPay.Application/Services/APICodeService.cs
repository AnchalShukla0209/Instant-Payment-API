using InstantPay.Application.DTOs;
using InstantPay.Application.Interfaces;
using InstantPay.Infrastructure.Sql.Entities;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace InstantPay.Application.Services
{
    public class APICodeService : IAPICodeService
    {
        private readonly AppDbContext _context;

        public APICodeService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<List<APICodeDropdownDto>> GetAPICodesForDropdown()
        {
            var query = from api in _context.APICodes
                        where api.IsActive && !api.IsDeleted
                        select new APICodeDropdownDto
                        {
                            Id = api.Id,
                            APICodeValue = api.APICodeValue,
                            Name = api.Name
                        };

            return await query.OrderBy(x => x.Name).ToListAsync();
        }
    }
}

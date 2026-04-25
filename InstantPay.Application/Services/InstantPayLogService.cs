using InstantPay.Application.Interfaces;
using InstantPay.Infrastructure.Sql.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InstantPay.Application.Services
{
    public class InstantPayLogService : IInstantPayLogService
    {
        private readonly AppDbContext _context;

        public InstantPayLogService(AppDbContext context)
        {
            _context = context;
        }

        public async Task AddLogAsync(string request, string response, string apiMode)
        {
            var log = new InstantPayLog
            {
                Request = request,
                Response = response,
                APIMode = apiMode,
                CreatedOn = DateTime.Now
            };

            _context.Tbl_InstantPayLogs.Add(log);
            await _context.SaveChangesAsync();
        }
    }
}

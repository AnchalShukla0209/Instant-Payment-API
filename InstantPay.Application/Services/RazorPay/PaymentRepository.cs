using InstantPay.Application.Interfaces.RazorPay;
using InstantPay.Infrastructure.Sql.Entities;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InstantPay.Application.Services.RazorPay
{
    public class PaymentRepository : IPaymentRepository
    {
        private readonly AppDbContext _context;

        public PaymentRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task Insert(Tblonlinepayment entity)
        {
            await _context.Tblonlinepayments.AddAsync(entity);
            await _context.SaveChangesAsync();
        }

        public async Task<Tblonlinepayment> GetByRazorpayOrderId(string orderId)
        {
            return await _context.Tblonlinepayments
                .FirstOrDefaultAsync(x => x.OrderId == orderId);
        }

        public async Task Update(Tblonlinepayment entity)
        {
            _context.Tblonlinepayments.Update(entity);
            await _context.SaveChangesAsync();
        }
    }
}

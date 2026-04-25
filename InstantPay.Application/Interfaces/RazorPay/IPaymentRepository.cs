using InstantPay.Infrastructure.Sql.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InstantPay.Application.Interfaces.RazorPay
{
    public interface IPaymentRepository
    {
        Task Insert(Tblonlinepayment entity);
        Task<Tblonlinepayment> GetByRazorpayOrderId(string orderId);
        Task Update(Tblonlinepayment entity);
    }
}

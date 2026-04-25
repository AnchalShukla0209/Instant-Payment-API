using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InstantPay.Application.IRepositry
{
    public interface IRechargeRepository
    {
        Task<string> Recharge(string mobile, string amount, string orderId, string companyId, string Type, string Optional="", string Optional1="");
    }
}

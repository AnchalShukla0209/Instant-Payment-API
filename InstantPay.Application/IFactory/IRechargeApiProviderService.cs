using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InstantPay.Application.IFactory
{
    public interface IRechargeApiProviderService
    {
        Task<string> Process(string provider, string mobile, string amount, string orderId, string companyId, string Type, string Optional, string Optional1, bool isStv = false);
    }
}

using InstantPay.Application.IFactory;
using InstantPay.Application.IRepositry;
using InstantPay.Application.Repositry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InstantPay.Application.Factory
{
    public class RechargeApiProviderService: IRechargeApiProviderService
    {
        private readonly IDictionary<string, IRechargeRepository> _providers;

        public RechargeApiProviderService(
            IcoreRechargeRepository icore,
            MroboticsRechargeRepository mrobotics,
            AmbikaRechargeRepository ambika,
            CyrusRechargeRepository cyrus
        )
        {
            _providers = new Dictionary<string, IRechargeRepository>
        {
            {"iqore", icore},
            {"mrobotics", mrobotics},
            {"ambika", ambika},
            {"cyrusre", cyrus}
        };
        }

        public async Task<string> Process(string provider, string mobile, string amount, string orderId, string companyId, string Type, string Optional, string Optional1, bool isStv = false)
        {
            if (!_providers.TryGetValue(provider.ToLower(), out var repo))
                throw new Exception($"Provider [{provider}] not supported");

            return await repo.Recharge(mobile, amount, orderId, companyId, Type, Optional, Optional1, isStv);
        }
    }
}

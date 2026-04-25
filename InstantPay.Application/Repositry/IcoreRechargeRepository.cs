using InstantPay.Application.IRepositry;
using InstantPay.Infrastructure.Sql.Entities;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InstantPay.Application.Repositry
{
    public class IcoreRechargeRepository : IRechargeRepository
    {
        private readonly IConfiguration _config;
        private readonly HttpClient _client;
        private readonly AppDbContext _Context;
        public IcoreRechargeRepository(IHttpClientFactory factory, IConfiguration config, AppDbContext context)
        {
            _client = factory.CreateClient();
            _config = config;
            _Context = context;
        }

        public async Task<string> Recharge(string mobile, string amount, string orderId, string companyId, string type, string Optional = "", string Optional1 = "")
        {
            var baseUrl = _config["RechargeApis:iCore:BaseUrl"];
            var signature = _config["RechargeApis:iCore:Signature"];

            string url = $"{baseUrl}/live?signature={signature}" +
                         $"&rnum={mobile}&ramt={amount}" +
                         $"&optr={companyId}&type={type}" +
                         $"&cack={orderId}&optional={Optional}&optional1={Optional1}";

            string apiResponse = string.Empty;

            try
            {
                var response = await _client.PostAsync(url, null);
                apiResponse = await response.Content.ReadAsStringAsync();
            }
            catch (Exception ex)
            {
                apiResponse = $"Error: {ex.Message}";
            }

            // Save Log
            var log = new Apilog
            {
                Request = url,
                Response = apiResponse,
                Apiname = "iCore",
                Reqdatae = DateTime.Now
            };

            _Context.Apilogs.Add(log);
            await _Context.SaveChangesAsync();

            return apiResponse;
        }


    }
}

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
    public class CyrusRechargeRepository : IRechargeRepository
    {
        private readonly IConfiguration _config;
        private readonly HttpClient _client;
        private readonly AppDbContext _Context;

        public CyrusRechargeRepository(IHttpClientFactory factory, IConfiguration config, AppDbContext context)
        {
            _client = factory.CreateClient();
            _config = config;
            _Context = context;
        }

        public async Task<string> Recharge(string mobile, string amount, string orderId, string companyId, string Type, string Optional = "", string Optional1 = "")
        {
            var baseUrl = _config["RechargeApis:Cyrus:BaseUrl"];
            var memberId = _config["RechargeApis:Cyrus:MemberId"];
            var pin = _config["RechargeApis:Cyrus:Pin"];
            var format = _config["RechargeApis:Cyrus:Format"];

            var url =
                $"{baseUrl}/api/recharge.aspx?memberid={memberId}&pin={pin}&number={mobile}&operator={companyId}&circle=19&amount={amount}&usertx={orderId}&format={format}";
            string apiResponse = string.Empty;

            try
            {
                apiResponse = await _client.GetStringAsync(url);
            }
            catch (Exception ex)
            {
                apiResponse = $"Error: {ex.Message}";
            }

            var log = new Apilog
            {
                Request = url,
                Response = apiResponse,
                Apiname = "Ambika",
                Reqdatae = DateTime.Now
            };

            _Context.Apilogs.Add(log);
            await _Context.SaveChangesAsync();

            return apiResponse;
        }
    }
}

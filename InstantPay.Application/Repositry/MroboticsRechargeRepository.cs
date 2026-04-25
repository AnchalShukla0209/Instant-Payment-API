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
    public class MroboticsRechargeRepository: IRechargeRepository
    {
        private readonly IConfiguration _config;
        private readonly HttpClient _client;
        private readonly AppDbContext _Context;

        public MroboticsRechargeRepository(IHttpClientFactory factory, IConfiguration config, AppDbContext context)
        {
            _client = factory.CreateClient();
            _config = config;
            _Context = context;
        }

        public async Task<string> Recharge(string mobile, string amount, string orderId, string companyId, string Type, string Optional = "", string Optional1 = "")
        {
            var token = _config["RechargeApis:Mrobotics:Token"];
            var baseUrl = _config["RechargeApis:Mrobotics:BaseUrl"];

            var url = $"{baseUrl}/api/recharge_get?api_token={token}&mobile_no={mobile}&amount={amount}&company_id={companyId}&order_id={orderId}&is_stv=false";

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

using Azure;
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
    public class AmbikaRechargeRepository : IRechargeRepository
    {
        private readonly IConfiguration _config;
        private readonly HttpClient _client;
        private readonly AppDbContext _Context;

        public AmbikaRechargeRepository(IHttpClientFactory factory, IConfiguration config, AppDbContext context)
        {
            _client = factory.CreateClient();
            _config = config;
            _Context = context;
        }

        public async Task<string> Recharge(string mobile, string amount, string orderId, string companyId, string Type, string Optional="", string Optional1="")
        {
            var baseUrl = _config["RechargeApis:Ambika:BaseUrl"];
            var userid = _config["RechargeApis:Ambika:UserId"];
            var token = _config["RechargeApis:Ambika:Token"];
            var pincode = _config["RechargeApis:Ambika:Pincode"];
            var geo = _config["RechargeApis:Ambika:GeoCode"];
            var format = _config["RechargeApis:Ambika:Format"];

            var url =
                $"{baseUrl}/API/TransactionAPI?UserID={userid}&Token={token}&Account={mobile}&Amount={amount}&SPKey={companyId}&APIRequestID={orderId}&GEOCode={geo}&CustomerNumber={mobile}&Pincode={pincode}&Format={format}";

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

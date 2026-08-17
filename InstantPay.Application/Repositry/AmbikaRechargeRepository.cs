using Azure;
using InstantPay.Application.IRepositry;
using InstantPay.Infrastructure.Sql.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
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
        private readonly IServiceProvider _serviceProvider;

        public AmbikaRechargeRepository(IHttpClientFactory factory, IConfiguration config, AppDbContext context, IServiceProvider serviceProvider)
        {
            _client = factory.CreateClient();
            _config = config;
            _Context = context;
            _serviceProvider = serviceProvider;
        }

        public async Task<string> Recharge(string mobile, string amount, string orderId, string companyId, string Type, string Optional="", string Optional1="", bool isStv = false)
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

            // Save Log using separate context to persist even if main transaction rolls back
            await SaveApiLogSeparately(url, apiResponse, "Ambika");

            return apiResponse;
        }

        private async Task SaveApiLogSeparately(string request, string response, string apiName)
        {
            try
            {
                // Create a new DbContext scope to bypass the current transaction
                using var scope = _serviceProvider.CreateScope();
                var logContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                
                var log = new Apilog
                {
                    Request = request,
                    Response = response,
                    Apiname = apiName,
                    Reqdatae = DateTime.Now
                };

                logContext.Apilogs.Add(log);
                await logContext.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                // Log the error but don't fail the main transaction
                Console.WriteLine($"Failed to save API log: {ex.Message}");
            }
        }
    }
}

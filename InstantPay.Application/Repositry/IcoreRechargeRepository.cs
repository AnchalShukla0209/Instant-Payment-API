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
    public class IcoreRechargeRepository : IRechargeRepository
    {
        private readonly IConfiguration _config;
        private readonly HttpClient _client;
        private readonly AppDbContext _Context;
        private readonly IServiceProvider _serviceProvider;
        public IcoreRechargeRepository(IHttpClientFactory factory, IConfiguration config, AppDbContext context, IServiceProvider serviceProvider)
        {
            _client = factory.CreateClient();
            _config = config;
            _Context = context;
            _serviceProvider = serviceProvider;
        }

        public async Task<string> Recharge(string mobile, string amount, string orderId, string companyId, string type, string Optional = "", string Optional1 = "", bool isStv = false)
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

            // Save Log using separate context to persist even if main transaction rolls back
            await SaveApiLogSeparately(url, apiResponse, "iCore");

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
                // Consider logging to file or external monitoring system
                Console.WriteLine($"Failed to save API log: {ex.Message}");
            }
        }


    }
}

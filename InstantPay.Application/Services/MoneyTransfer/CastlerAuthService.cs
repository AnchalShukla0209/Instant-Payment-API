using InstantPay.Application.Interfaces.MoneyTransfer.Castler;
using InstantPay.Infrastructure.Sql.Entities;
using InstantPay.SharedKernel.Entity.CastlerConfigDTO;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Microsoft.AspNetCore.Hosting.Internal.HostingApplication;

namespace InstantPay.Application.Services.MoneyTransfer
{
    public class CastlerAuthService : ICastlerAuthService
    {
        private readonly HttpClient _httpClient;
        private readonly CastlerConfig _config;
        private readonly AppDbContext _repo;

        public CastlerAuthService(HttpClient httpClient, IOptions<CastlerConfig> config, AppDbContext repo)
        {
            _httpClient = httpClient;
            _config = config.Value;
            _repo = repo;
        }

        public async Task<string> GenerateToken()
        {
            var request = new HttpRequestMessage(HttpMethod.Post, $"{_config.BaseUrl}api/v1/auth/api-credential/token");
            request.Headers.Add("x-api-key", _config.XApiKey);

            var body = new
            {
                apiKey = _config.ApiKey,
                apiSecret = _config.ApiSecret
            };

            request.Content = new StringContent(JsonConvert.SerializeObject(body), Encoding.UTF8, "application/json");

            var response = await _httpClient.SendAsync(request);
            var json = await response.Content.ReadAsStringAsync();

            var logdetail = new Apilog
            {
                Apiname = "Token-Castler",
                Reqdatae = DateTime.Now,
                Request = JsonConvert.SerializeObject(body),
                Response = json
            };
            _repo.Apilogs.Add(logdetail);
            await _repo.SaveChangesAsync();

            dynamic result = JsonConvert.DeserializeObject(json);

            string token = result?.result?.token;

            if (!string.IsNullOrEmpty(token))
            {
                var existing = await _repo.CastlerToken.FirstOrDefaultAsync();

                if (existing != null)
                {
                    existing.CastlerAccessToken = token;
                    existing.UpdatedOn = DateTime.Now;
                }
                else
                {
                    _repo.CastlerToken.Add(new CastlerToken
                    {
                        CastlerAccessToken = token,
                        CreatedOn = DateTime.Now
                    });
                }
                await _repo.SaveChangesAsync(); 
            }

            return token;
        }

        public async Task<string> GetToken()
        {
            var data = await _repo.CastlerToken.FirstOrDefaultAsync();
            if(data == null)
            {
                return await GenerateToken();
            }
            else
            {
                return data.CastlerAccessToken ?? "";
            }
                
        }
    }
}

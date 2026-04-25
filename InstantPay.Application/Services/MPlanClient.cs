using InstantPay.Application.Interfaces;
using InstantPay.SharedKernel.RequestPayload;
using InstantPay.SharedKernel.Results;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace InstantPay.Application.Services
{
    public class MPlanClient : IMPlanClient
    {
       
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;

        public MPlanClient(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _configuration = configuration;
        }

        public async Task<object> GetRechargePlansAsync(PlanRequestPayload payload)
        {
            var json = "";
            try
            {
                string ProviderId = GetProviderId(payload.operatorName);
                var baseUrl = _configuration["A2ZRecharge:BaseUrl"];
                var rechargePlanUrl = _configuration["A2ZRecharge:RechargePlanUrl"];

                var finalUrl = $"{baseUrl.TrimEnd('/')}/{rechargePlanUrl.TrimStart('/')}";


                var url = $"{finalUrl}?api_token={_configuration["A2ZRecharge:ApiToken"]}" +
                          $"&userId={_configuration["A2ZRecharge:UserId"]}" +
                          $"&secretKey={_configuration["A2ZRecharge:SecretKey"]}" +
                          $"&mobile_number={payload.tel}" +
                          $"&provider={ProviderId}";

                Console.WriteLine("FINAL URL: " + url);

                var response = await _httpClient.GetAsync(url);

                if (!response.IsSuccessStatusCode)
                {
                    return new
                    {
                        code = (int)response.StatusCode,
                        message = "Failed to fetch recharge plans"
                    };
                }

                json = await response.Content.ReadAsStringAsync();

                var result = JsonSerializer.Deserialize<object>(
                    json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
                );

                return new
                {
                    code = 200,
                    data = new
                    {
                        data = result,
                        message = "Recharge Plans"
                    }
                };
            }
            catch (Exception ex)
            {
                // Log error if needed
                return new
                {
                    code = 500,
                    message = json
                };
            }
        }

        public string GetProviderId(string operatorName)
        {
            if (string.IsNullOrWhiteSpace(operatorName))
                throw new ArgumentException("Operator name is required");

            switch (operatorName.Trim().ToUpper())
            {
                case "AIRTEL":
                    return "1";
                case "VODAFONE":
                case "VI":
                    return "2";
                case "BSNL":
                    return "8";
                case "JIO":
                    return "112";
                default:
                    return "0";
            }
        }
    }
}

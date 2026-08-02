using InstantPay.Application.Interfaces.A2Z;
using InstantPay.SharedKernel.RequestPayload.A2Z;
using InstantPay.SharedKernel.Results.A2Z;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace InstantPay.Application.Services.A2Z
{
    public class A2ZClient : IA2ZClient
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;

        public A2ZClient(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _configuration = configuration;
        }

        public async Task<A2ZRechargePlanResponse> GetRechargePlansAsync(A2ZRechargePlanRequest payload)
        {
            var baseUrl = _configuration["A2ZRecharge:BaseUrl"];
            var rechargeUrl = _configuration["A2ZRecharge:RechargePlanUrl"];

            var apiToken = _configuration["A2ZRecharge:ApiToken"];
            var userId = _configuration["A2ZRecharge:UserId"];
            var secretKey = _configuration["A2ZRecharge:SecretKey"];

            var query = new Dictionary<string, string>
            {
                { "api_token", apiToken },
                { "userId", userId },
                { "secretKey", secretKey },
                { "mobile_number", payload.mobile_number },
                { "provider", payload.provider }
            };

            var url = QueryHelpers.AddQueryString(baseUrl + rechargeUrl, query);

            var response = await _httpClient.GetAsync(url);

            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();

            return JsonSerializer.Deserialize<A2ZRechargePlanResponse>(
                json,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                }
            );
        }

        public async Task<A2ZCreditCardBillFetchResponse> FetchCreditCardBillAsync(A2ZCreditCardBillFetchRequest request)
        {
            var apiToken = _configuration["A2ZRecharge:ApiToken"];
            var userId = _configuration["A2ZRecharge:UserId"];
            var secretKey = _configuration["A2ZRecharge:SecretKey"];

            var payload = new Dictionary<string, object>
            {
                { "api_token", apiToken },
                { "provider", request.provider },
                { "number", request.number },
                { "userId", userId },
                { "secretKey", secretKey },
                { "customerMobileNumber", request.customerMobileNumber },
                { "field_one", request.customerMobileNumber }
            };

            var requestJson = JsonSerializer.Serialize(payload);
            using var content = new StringContent(requestJson, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync("fetch/bill-details", content);
            response.EnsureSuccessStatusCode();

            var responseJson = await response.Content.ReadAsStringAsync();

            return JsonSerializer.Deserialize<A2ZCreditCardBillFetchResponse>(
                responseJson,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                }
            );
        }
    }
}

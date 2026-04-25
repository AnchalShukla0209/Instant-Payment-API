using InstantPay.Application.Interfaces;
using InstantPay.SharedKernel.RandomNumberGenerator;
using InstantPay.SharedKernel.RequestPayload;
using InstantPay.SharedKernel.Results;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Json;
using System.Text;
using System.Threading.Tasks;

namespace InstantPay.Application.Services
{
    public class BillInfoService : IBillInfoService
    {
        private readonly IConfiguration _config;
        private readonly IHttpClientFactory _httpClientFactory;

        public BillInfoService(IHttpClientFactory httpClientFactory, IConfiguration config)
        {
            _httpClientFactory = httpClientFactory;
            _config = config;
        }

        public async Task<BillFetchResponseDto> FetchBillAsync(BillFetchRequestDto request)
        {
            var client = _httpClientFactory.CreateClient("iQore");
            var baseUrl = _config["RechargeApis:iCore:BaseUrl"];
            var signature = _config["RechargeApis:iCore:Signature"];

            var url =
                $"{baseUrl}/binfo/?" +
                $"signature={signature}" +
                $"&cack={ReferenceGenerator.GenerateCustomerRefNo()}" +
                $"&tel={request.Mobile}" +
                $"&optional={request.optional}" +
                $"&operator={request.Operator}" +
                $"&type={request.Type}";

            using var response = await client.PostAsync(url, null);
            response.EnsureSuccessStatusCode();

            return await response.Content.ReadFromJsonAsync<BillFetchResponseDto>();
        }
    }
}

using InstantPay.Application.Interfaces;
using InstantPay.SharedKernel.RandomNumberGenerator;
using InstantPay.SharedKernel.RequestPayload;
using InstantPay.SharedKernel.Results;
using Microsoft.Extensions.Configuration;
using Org.BouncyCastle.Asn1.Ocsp;
using System;
using System.Buffers.Text;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Json;
using System.Text;
using System.Threading.Tasks;

namespace InstantPay.Application.Services
{
    public class InsuranceInfoService : IInsuranceInfoService
    {
        private readonly IConfiguration _config;
        private readonly IHttpClientFactory _httpClientFactory;

        public InsuranceInfoService(IHttpClientFactory httpClientFactory, IConfiguration config)
        {
            _httpClientFactory = httpClientFactory;
            _config = config;
        }

        public async Task<InsuranceFetchResponseDto> FetchInsuranceAsync(
        InsuranceFetchRequestDto request)
        {
            var client = _httpClientFactory.CreateClient("iQore");
            var baseUrl = _config["RechargeApis:iCore:BaseUrl"];
            var signature = _config["RechargeApis:iCore:Signature"];

            var url =
                $"{baseUrl}/binfo/?" +
                $"signature={signature}" +
                $"&cack={ReferenceGenerator.GenerateCustomerRefNo()}" +
                $"&tel={request.PolicyNumber}" +
                $"&optr={request.Operator}" +
                $"&optional={request.optional}" +
                $"&type=ins";

            if (!string.IsNullOrEmpty(request.Email))
                url += $"&optional={request.Email}";

            if (!string.IsNullOrEmpty(request.Dob))
                url += $"&additional1={request.Dob}";

            using var response = await client.PostAsync(url, null);
            response.EnsureSuccessStatusCode();

            return await response.Content
                .ReadFromJsonAsync<InsuranceFetchResponseDto>();
        }
    }
}

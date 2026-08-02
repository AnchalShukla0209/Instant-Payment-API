using InstantPay.Application.Interfaces;
using InstantPay.SharedKernel.RandomNumberGenerator;
using InstantPay.SharedKernel.RequestPayload;
using InstantPay.SharedKernel.Results;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text;
using System.Threading.Tasks;

namespace InstantPay.Application.Services
{
    public class BillInfoService : IBillInfoService
    {
        private readonly IConfiguration _config;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IInstantPayLogService _logService;
        public BillInfoService(IHttpClientFactory httpClientFactory, IConfiguration config, IInstantPayLogService logService)
        {
            _httpClientFactory = httpClientFactory;
            _config = config;
            _logService = logService;
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

            string rawContent = null;

            try
            {
                using var response = await client.PostAsync(url, null);
                rawContent = await response.Content.ReadAsStringAsync();
                response.EnsureSuccessStatusCode();

                await _logService.AddLogAsync("", rawContent, $"BILL FETCH");

                var trimmed = rawContent.TrimStart();

                if (trimmed.StartsWith("{") || trimmed.StartsWith("["))
                {
                    try
                    {
                        return JsonSerializer.Deserialize<BillFetchResponseDto>(rawContent,
                            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                            ?? new BillFetchResponseDto { RawContent = rawContent };
                    }
                    catch (JsonException ex)
                    {
                        return new BillFetchResponseDto
                        {
                            Status = 0,
                            Mobile = request.Mobile,
                            Operator = request.Operator,
                            RawContent = rawContent,
                            Rdata = new List<BillData> { new BillData { Status = 0, Desc = $"Invalid JSON response: {ex.Message}" } }
                        };
                    }
                }

                return ParsePipeDelimitedResponse(rawContent, request.Mobile, request.Operator);
            }
            catch (OperationCanceledException)
            {
                return new BillFetchResponseDto
                {
                    Status = 0,
                    Mobile = request.Mobile,
                    Operator = request.Operator,
                    RawContent = rawContent,
                    Rdata = new List<BillData> { new BillData { Status = 0, Desc = "Bill fetch request timed out." } }
                };
            }
            catch (HttpRequestException ex)
            {
                return new BillFetchResponseDto
                {
                    Status = 0,
                    Mobile = request.Mobile,
                    Operator = request.Operator,
                    RawContent = rawContent,
                    Rdata = new List<BillData> { new BillData { Status = 0, Desc = $"Unable to reach bill provider: {ex.Message}" } }
                };
            }
        }

        private static BillFetchResponseDto ParsePipeDelimitedResponse(string raw, string mobile, string operatorCode)
        {
            var parts = raw.Trim().Split('|');
            int.TryParse(parts[0], out var status);

            var result = new BillFetchResponseDto
            {
                Status = status,
                Mobile = mobile,
                Operator = operatorCode,
                Rdata = new List<BillData>()
            };

            if (parts.Length > 1)
            {
                result.Rdata.Add(new BillData
                {
                    Status = status,
                    Desc = parts.Length > 1 ? parts[1] : null,
                    CustomerName = parts.Length > 2 ? parts[2] : null,
                    Billamount = parts.Length > 3 ? parts[3] : null,
                    Billdate = parts.Length > 4 ? parts[4] : null,
                    Duedate = parts.Length > 5 ? parts[5] : null,
                    BillNumber = parts.Length > 6 ? parts[6] : null
                });
            }

            return result;
        }
    }
}

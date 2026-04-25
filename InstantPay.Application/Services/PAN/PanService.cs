using InstantPay.Application.Interfaces.PAN;
using InstantPay.SharedKernel.AppSettingsConfiguration;
using InstantPay.SharedKernel.Results.PAN;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace InstantPay.Application.Services.PAN
{
    public class PanService : IPanService
    {
        private readonly HttpClient _httpClient;
        private readonly PanApiSettings _settings;

        public PanService(HttpClient httpClient, IOptions<PanApiSettings> settings)
        {
            _httpClient = httpClient;
            _settings = settings.Value;
        }

        public async Task<PanVerifyResponse> VerifyPanAsync(string panNumber)
        {
            var requestBody = new
            {
                key = _settings.ApiKey,
                id_number = panNumber
            };

            var content = new StringContent(
                JsonSerializer.Serialize(requestBody),
                Encoding.UTF8,
                "application/json"
            );

            var response = await _httpClient.PostAsync(_settings.BaseUrl, content);

            if (!response.IsSuccessStatusCode)
            {
                return new PanVerifyResponse
                {
                    Success = false,
                    Message = "API request failed"
                };
            }

            if (!Regex.IsMatch(panNumber, "^[A-Z]{5}[0-9]{4}[A-Z]{1}$"))
            {
                return new PanVerifyResponse
                {
                    Success = false,
                    Message = "Invalid PAN format"
                };
            }

            var jsonString = await response.Content.ReadAsStringAsync();
            var json = JObject.Parse(jsonString);

            var status = json["status"]?.ToString();

            if (status?.ToLower() == "success")
            {
                return new PanVerifyResponse
                {
                    Success = true,
                    Name = json["data"]?["full_name"]?.ToString()
                };
            }

            return new PanVerifyResponse
            {
                Success = false,
                Message = "Invalid PAN"
            };
        }
    }
}

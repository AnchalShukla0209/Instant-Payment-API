using InstantPay.Application.Interfaces.Aadhaar;
using InstantPay.SharedKernel.AppSettingsConfiguration;
using InstantPay.SharedKernel.Results.Aadhaar;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace InstantPay.Application.Services.Aadhaar
{
    public class AadhaarService : IAadhaarService
    {
        private readonly HttpClient _httpClient;
        private readonly AadhaarApiSettings _settings;

        public AadhaarService(HttpClient httpClient, IOptions<AadhaarApiSettings> settings)
        {
            _httpClient = httpClient;
            _settings = settings.Value;
        }

        public async Task<AadhaarVerifyResponse> VerifyAadhaarAsync(string aadhaarNumber)
        {
            aadhaarNumber = aadhaarNumber?.Trim() ?? string.Empty;
            if (!Regex.IsMatch(aadhaarNumber, "^[0-9]{12}$"))
            {
                return new AadhaarVerifyResponse
                {
                    Success = false,
                    Message = "Invalid Aadhaar format"
                };
            }

            var requestBody = new
            {
                key = _settings.ApiKey,
                id_number = aadhaarNumber
            };

            var content = new StringContent(
                JsonSerializer.Serialize(requestBody),
                Encoding.UTF8,
                "application/json"
            );

            var response = await _httpClient.PostAsync(_settings.BaseUrl, content);

            if (!response.IsSuccessStatusCode)
            {
                return new AadhaarVerifyResponse
                {
                    Success = false,
                    Message = "API request failed"
                };
            }

            var jsonString = await response.Content.ReadAsStringAsync();
            var json = JObject.Parse(jsonString);

            var statusCode = json["status_code"]?.ToString();
            var data = json["data"];
            var status = data?["status"]?.ToString();

            if (statusCode == "200" && string.Equals(status, "success_aadhaar", StringComparison.OrdinalIgnoreCase))
            {
                return new AadhaarVerifyResponse
                {
                    Success = true,
                    State = data?["state"]?.ToString(),
                    AgeRange = data?["age_range"]?.ToString(),
                    Gender = data?["gender"]?.ToString(),
                    MaskedMobileNumber = data?["masked_mobile_number"]?.ToString()
                };
            }

            return new AadhaarVerifyResponse
            {
                Success = false,
                Message = json["message"]?.ToString() ?? "Invalid Aadhaar number"
            };
        }
    }
}

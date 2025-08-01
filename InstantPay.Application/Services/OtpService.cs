using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using InstantPay.Application.Interfaces;
using System.Net.NetworkInformation;

namespace InstantPay.Application.Services
{
    public class OtpService : IOtpService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;

        public OtpService(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _configuration = configuration;
        }

        public async Task<string> SendOtpAsync(string mobile, string otp)
        {
            var baseUrl = _configuration["OTP:BaseUrl"];
            var authKey = _configuration["OTP:AuthKey"];
            var templateId = _configuration["OTP:TemplateId"];

            var url = $"{baseUrl}?template_id={templateId}&mobile=91{mobile}&otp={otp}&otp_length=4";
            var body = new StringContent("{\"Param1\":\"value1\",\"Param2\":\"value2\",\"Param3\":\"value3\"}", Encoding.UTF8, "application/json");

            var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Headers.Add("authkey", authKey);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            request.Content = body;

            var response = await _httpClient.SendAsync(request);
            return await response.Content.ReadAsStringAsync();
        }

        public async Task<string> GetMacAddress()
        {
            string sMacAddress = string.Empty;
            try
            {
                NetworkInterface[] nics = NetworkInterface.GetAllNetworkInterfaces();

                foreach (NetworkInterface adapter in nics)
                {
                    if (sMacAddress == string.Empty)
                    {
                        IPInterfaceProperties properties = adapter.GetIPProperties();
                        sMacAddress = adapter.GetPhysicalAddress().ToString();

                        if (!string.IsNullOrEmpty(sMacAddress))
                            break;
                    }
                }
            }
            catch
            {
                sMacAddress = "UNKNOWN";
            }

            return sMacAddress;
        }

    }

}

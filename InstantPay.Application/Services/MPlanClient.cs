using InstantPay.Application.Interfaces;
using InstantPay.SharedKernel.RequestPayload;
using InstantPay.SharedKernel.Results;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
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

        public async Task<string> GetRechargePlansAsync(PlanRequestPayload payload)
        {
            var json = "";
            try
            {
                var mplanSection = _configuration.GetSection("MPlan");
                var apiKey = mplanSection.Exists() ? mplanSection["ApiKey"] : null;

                // Fallback to hardcoded key if config is missing
                if (string.IsNullOrEmpty(apiKey))
                {
                    apiKey = "3a527a8f2b21e286edd52ea46424b287";
                    Console.WriteLine("Using hardcoded API key");
                }

                Console.WriteLine($"ApiKey: {apiKey}");

                var url = $"plans.php?apikey={apiKey}" +
                          $"&offer={payload.offer}" +
                          $"&tel={payload.tel}" +
                          $"&operator={payload.operatorName}";

                Console.WriteLine("URL: " + url);

                var response = await _httpClient.PostAsync(url, null);

                if (!response.IsSuccessStatusCode)
                {
                    return JsonConvert.SerializeObject(new
                    {
                        code = (int)response.StatusCode,
                        data = new
                        {
                            message = "Failed to fetch recharge plans",
                            data = (object)null
                        }
                    });
                }

                json = await response.Content.ReadAsStringAsync();
                Console.WriteLine("Response JSON: " + json);

                var apiResponse = JObject.Parse(json);

                var formattedResponse = new
                {
                    code = 200,
                    data = new
                    {
                        message = "Recharge Plans",
                        data = new
                        {
                            tel = apiResponse["tel"]?.ToString(),
                            @operator = apiResponse["operator"]?.ToString(),
                            message = "Recharge Plans",
                            records = apiResponse["records"],
                            status = apiResponse["status"]?.ToObject<int>()
                        }
                    }
                };

                return JsonConvert.SerializeObject(formattedResponse);
            }
            catch (Exception ex)
            {
                // Log error if needed
                Console.WriteLine("Exception: " + ex.Message);
                return JsonConvert.SerializeObject(new
                {
                    code = 500,
                    data = new
                    {
                        message = ex.Message,
                        data = (object)null
                    }
                });
            }
        }

        public async Task<string> GetRechargePlansNewAsync(PlanRequestPayload payload)
        {
            var json = "";
            try
            {
                payload.offer = "roffer";
                var mplanSection = _configuration.GetSection("MPlan");
                var apiKey = mplanSection.Exists() ? mplanSection["ApiKey"] : null;

                // Fallback to hardcoded key if config is missing
                if (string.IsNullOrEmpty(apiKey))
                {
                    apiKey = "3a527a8f2b21e286edd52ea46424b287";
                    Console.WriteLine("Using hardcoded API key");
                }

                Console.WriteLine($"ApiKey: {apiKey}");

                var url = $"plans.php?apikey={apiKey}" +
                          $"&offer={payload.offer}" +
                          $"&tel={payload.tel}" +
                          $"&operator={payload.operatorName}";

                Console.WriteLine("URL: " + url);

                var response = await _httpClient.PostAsync(url, null);

                if (!response.IsSuccessStatusCode)
                {
                    return JsonConvert.SerializeObject(new
                    {
                        code = (int)response.StatusCode,
                        message = "Failed to fetch recharge plans",
                        data = (object)null
                    });
                }

                json = await response.Content.ReadAsStringAsync();
                Console.WriteLine("Response JSON: " + json);

                var apiResponse = JObject.Parse(json);

                // Handle both flat {tel,operator,records} and nested {data:{tel,operator,records}} structures
                JObject dataNode = apiResponse["data"] is JObject nested ? nested : apiResponse;

                var rawRecords = (dataNode["records"] ?? dataNode["Records"]) as JArray;
                var tel = dataNode["tel"]?.ToString() ?? dataNode["Tel"]?.ToString();
                var operatorName = dataNode["operator"]?.ToString() ?? dataNode["Operator"]?.ToString();

                var plans = new List<object>();

                if (rawRecords != null)
                {
                    foreach (var record in rawRecords)
                    {
                        var price = record["rs"]?.ToString() ?? "";
                        var desc = record["desc"]?.ToString() ?? "";
                        var (description, shortInfo, validity, data, callsVal, sms, benefits, category) = ParsePlanDesc(price, desc);

                        plans.Add(new
                        {
                            price,
                            validity,
                            data,
                            calls = callsVal,
                            sms,
                            description,
                            shortInfo,
                            benefits,
                            category
                        });
                    }
                }

                var formattedResponse = new
                {
                    code = 200,
                    message = "Recharge Plans",
                    data = new
                    {
                        tel,
                        @operator = operatorName,
                        totalPlans = plans.Count,
                        plans,
                        _rawApiResponse = json   // TODO: remove after confirming response structure
                    }
                };

                return JsonConvert.SerializeObject(formattedResponse);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception: " + ex.Message);
                return JsonConvert.SerializeObject(new
                {
                    code = 500,
                    message = ex.Message,
                    data = (object)null
                });
            }
        }

        private static (string description, string shortInfo, string validity, string data, string calls, string sms, List<string> benefits, string category) ParsePlanDesc(string price, string desc)
        {
            string description = desc;
            string shortInfo = "";
            string validity = "";
            string data = "";
            string calls = "";
            string sms = "";
            var benefits = new List<string>();
            string category = "Other";

            var splitMatch = Regex.Match(desc, @"^(.+),(?:RC\d+|\d+)=(.+)$");
            if (splitMatch.Success)
            {
                description = splitMatch.Groups[1].Value.Trim();
                shortInfo = splitMatch.Groups[2].Value.Trim();
            }

            var fullText = desc.ToLower();
            var descText = description.ToLower();
            var infoText = shortInfo.ToLower();

            // Validity
            if (Regex.IsMatch(infoText, @"\b1y\b"))
                validity = "365 Days";
            else if (Regex.IsMatch(infoText, @"\b365d\b"))
                validity = "365 Days";
            else if (Regex.IsMatch(fullText, @"\b1\s*din\s+tak\b"))
                validity = "1 Day";
            else
            {
                var m = Regex.Match(descText, @"(?:for\s+)?(\d+)\s*days?\b");
                if (m.Success)
                    validity = $"{m.Groups[1].Value} Days";
                else
                {
                    m = Regex.Match(fullText, @"(\d+)\s*din\s+tak");
                    if (m.Success)
                        validity = $"{m.Groups[1].Value} Days";
                    else
                    {
                        m = Regex.Match(infoText, @"\b(\d+)d\b");
                        if (m.Success)
                            validity = $"{m.Groups[1].Value} Days";
                    }
                }
            }

            // Data — check specific GB/day quota first, then unlimited, then total GB
            var dataPerDayMatch = Regex.Match(fullText, @"(\d+(?:\.\d+)?)\s*gb\s*/\s*d(?:ay)?\b");
            if (dataPerDayMatch.Success)
                data = $"{dataPerDayMatch.Groups[1].Value}GB/Day";
            else if (Regex.IsMatch(fullText, @"unlimited\s+(?:4g|5g|data)|ul\s+(?:4g|5g)\s+data|unltd\s+(?:4g|5g|data)"))
                data = "Unlimited";
            else
            {
                var totalDataMatch = Regex.Match(fullText, @"(\d+(?:\.\d+)?)\s*gb\b");
                if (totalDataMatch.Success)
                    data = $"{totalDataMatch.Groups[1].Value}GB";
            }

            // Calls
            if (Regex.IsMatch(fullText, @"unlimited\s+calls?|ul\s+cl\b|ulcl\b|unltd\s+call"))
                calls = "Unlimited";

            // SMS
            var smsMatch = Regex.Match(fullText, @"(\d+)\s*sms\s*/\s*d(?:ay)?");
            if (smsMatch.Success)
                sms = $"{smsMatch.Groups[1].Value} SMS/Day";

            // OTT / streaming benefits
            if (fullText.Contains("jiohotstar") || fullText.Contains("hotstar"))
                benefits.Add("JioHotstar");
            if (fullText.Contains("airtel xstream") || fullText.Contains("xstream play"))
                benefits.Add("Airtel Xstream Play");
            if (fullText.Contains("apple music"))
                benefits.Add("Apple Music");
            if (fullText.Contains("netflix"))
                benefits.Add("Netflix");
            if (fullText.Contains("amazon prime") || fullText.Contains("prime video"))
                benefits.Add("Amazon Prime");
            if (fullText.Contains("disney+") || fullText.Contains("disney plus"))
                benefits.Add("Disney+");

            // Category
            if (validity == "1 Day")
                category = "Daily";
            else if (validity == "365 Days")
                category = "Annual";
            else if (benefits.Count > 0 && !string.IsNullOrEmpty(data) && calls == "Unlimited")
                category = "Combo";
            else if (benefits.Count > 0 && !string.IsNullOrEmpty(data))
                category = "Entertainment";
            else if (calls == "Unlimited" && !string.IsNullOrEmpty(data))
                category = "Unlimited";
            else if (!string.IsNullOrEmpty(data))
                category = "Data";
            else if (calls == "Unlimited")
                category = "Voice";

            return (description, shortInfo, validity, data, calls, sms, benefits, category);
        }
    }
}

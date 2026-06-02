using InstantPay.Application.Interfaces;
using InstantPay.Infrastructure.Sql.Entities;
using InstantPay.SharedKernel.RequestPayload.WhatsApp;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace InstantPay.Application.Services
{
    public class WhatsAppService : IWhatsAppService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _config;
        private readonly AppDbContext _context;
        private readonly ILogger<WhatsAppService> _logger;

        private const int BatchSize = 100;

        private static readonly JsonSerializerOptions JsonOpts = new()
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        public WhatsAppService(
            HttpClient httpClient,
            IConfiguration config,
            AppDbContext context,
            ILogger<WhatsAppService> logger)
        {
            _httpClient = httpClient;
            _config = config;
            _context = context;
            _logger = logger;
        }

        public async Task<WhatsAppBroadcastResult> SendBroadcastMessageAsync(WhatsAppBroadcastRequest request)
        {
            var result = new WhatsAppBroadcastResult { SentAt = DateTime.UtcNow };

            try
            {
                if (string.IsNullOrWhiteSpace(request.Link))
                {
                    result.Success = false;
                    result.Message = "Link cannot be empty";
                    return result;
                }

                var authKey = _config["WhatsApp:Msg91AuthKey"];
                var integratedNumber = _config["WhatsApp:Msg91IntegratedNumber"];

                if (string.IsNullOrWhiteSpace(authKey))
                    throw new InvalidOperationException("WhatsApp:Msg91AuthKey is not configured.");
                if (string.IsNullOrWhiteSpace(integratedNumber))
                    throw new InvalidOperationException("WhatsApp:Msg91IntegratedNumber is not configured.");

                var templateName = !string.IsNullOrWhiteSpace(request.TemplateName)
                    ? request.TemplateName
                    : _config.GetValue<string>("WhatsApp:TemplateName") ?? "ins_pay_upd";
                var templateLang = !string.IsNullOrWhiteSpace(request.LanguageCode)
                    ? request.LanguageCode
                    : _config.GetValue<string>("WhatsApp:TemplateLanguageCode") ?? "hi";
                var bulkApiUrl = _config.GetValue<string>("WhatsApp:BulkApiUrl")
                    ?? "https://control.msg91.com/api/v5/whatsapp/whatsapp-outbound-message/bulk/";

                // Fetch users from database
                var usersQuery = _context.TblUsers.AsQueryable();
                if (request.SendToActiveUsersOnly == true)
                    usersQuery = usersQuery.Where(u => u.Status == "Active" || u.Status == "active");

                var users = await usersQuery
                    .Where(u => !string.IsNullOrWhiteSpace(u.Phone))
                    .Select(u => new { u.Phone, u.Name })
                    .ToListAsync();

                result.TotalUsers = users.Count;

                if (result.TotalUsers == 0)
                {
                    result.Success = false;
                    result.Message = "No users found to send message";
                    return result;
                }

                // Format and de-duplicate recipients
                var recipients = users
                    .Select(u => new
                    {
                        Phone = FormatPhoneNumber(u.Phone),
                        Name = string.IsNullOrWhiteSpace(u.Name) ? "User" : u.Name.Trim()
                    })
                    .Where(u => u.Phone != null)
                    .GroupBy(u => u.Phone)
                    .Select(g => g.First())
                    .ToList();

                _logger.LogInformation(
                    "Sending WhatsApp template '{Template}' to {Count} users via MSG91 in batches of {BatchSize}",
                    templateName, recipients.Count, BatchSize);

                // Split into batches
                var batches = recipients
                    .Select((r, i) => new { r, i })
                    .GroupBy(x => x.i / BatchSize)
                    .Select(g => g.Select(x => x.r).ToList())
                    .ToList();

                int batchNumber = 0;
                foreach (var batch in batches)
                {
                    batchNumber++;
                    try
                    {
                        var toAndComponents = batch.Select(u => new Msg91Recipient
                        {
                            To = new List<string> { u.Phone },
                            Components = new Msg91Components
                            {
                                Body1 = new Msg91ComponentValue { Value = u.Name },
                                Body2 = new Msg91ComponentValue { Value = request.Link }
                            }
                        }).ToList();

                        var payload = new Msg91BulkRequest
                        {
                            IntegratedNumber = integratedNumber,
                            ContentType = "template",
                            Payload = new Msg91Payload
                            {
                                Type = "template",
                                Template = new Msg91Template
                                {
                                    Name = templateName,
                                    Language = new Msg91Language { Code = templateLang },
                                    ToAndComponents = toAndComponents
                                },
                                MessagingProduct = "whatsapp"
                            }
                        };

                        var json = JsonSerializer.Serialize(payload, JsonOpts);

                        using var httpReq = new HttpRequestMessage(HttpMethod.Post, bulkApiUrl);
                        httpReq.Headers.TryAddWithoutValidation("accept", "application/json");
                        httpReq.Headers.TryAddWithoutValidation("authkey", authKey);
                        httpReq.Content = new StringContent(json, Encoding.UTF8, "application/json");

                        _logger.LogInformation(
                            "Dispatching batch {BatchNo}/{Total} ({Count} recipients)",
                            batchNumber, batches.Count, batch.Count);

                        var response = await _httpClient.SendAsync(httpReq);
                        var responseBody = await response.Content.ReadAsStringAsync();

                        _logger.LogInformation(
                            "MSG91 batch {BatchNo} [{StatusCode}]: {Body}",
                            batchNumber, (int)response.StatusCode, responseBody);

                        if (response.IsSuccessStatusCode)
                        {
                            result.SuccessfulSends += batch.Count;
                        }
                        else
                        {
                            result.FailedSends += batch.Count;
                            result.FailedPhoneNumbers.AddRange(batch.Select(b => b.Phone));
                            _logger.LogError(
                                "MSG91 batch {BatchNo} failed [{StatusCode}]: {Body}",
                                batchNumber, (int)response.StatusCode, responseBody);
                        }
                    }
                    catch (Exception ex)
                    {
                        result.FailedSends += batch.Count;
                        result.FailedPhoneNumbers.AddRange(batch.Select(b => b.Phone));
                        _logger.LogError(ex, "Exception in batch {BatchNo} of {Count} messages", batchNumber, batch.Count);
                    }
                }

                result.Success = result.SuccessfulSends > 0;
                result.Message = result.Success
                    ? $"Template queued for {result.SuccessfulSends} users. Failed: {result.FailedSends}."
                    : "Failed to queue message for any user.";

                await LogBroadcastAsync(request, result);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending WhatsApp broadcast via MSG91");
                result.Success = false;
                result.Message = $"An error occurred: {ex.Message}";
                return result;
            }
        }

        private static string FormatPhoneNumber(string phone)
        {
            if (string.IsNullOrWhiteSpace(phone))
                return null;

            var digits = System.Text.RegularExpressions.Regex.Replace(phone, @"[^\d]", "");

            // Normalise to 10-digit local number first
            if (digits.Length == 11 && digits.StartsWith("0"))
                digits = digits.Substring(1);
            else if (digits.Length == 12 && digits.StartsWith("91"))
                digits = digits.Substring(2);
            else if (digits.Length == 13 && digits.StartsWith("091"))
                digits = digits.Substring(3);

            if (digits.Length != 10)
                return null;

            // MSG91 expects country-code prefix without '+'
            return $"91{digits}";
        }

        private async Task LogBroadcastAsync(WhatsAppBroadcastRequest request, WhatsAppBroadcastResult result)
        {
            try
            {
                var log = new Apilog
                {
                    Apiname = "WhatsAppBroadcast",
                    Reqdatae = DateTime.Now,
                    Request = JsonSerializer.Serialize(new
                    {
                        request.Link,
                        request.SendToActiveUsersOnly
                    }),
                    Response = JsonSerializer.Serialize(new
                    {
                        result.Success,
                        result.TotalUsers,
                        result.SuccessfulSends,
                        result.FailedSends,
                        result.Message,
                        result.SentAt
                    })
                };

                _context.Apilogs.Add(log);
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error logging WhatsApp broadcast");
            }
        }

        // ── MSG91 API internal DTOs ──────────────────────────────────────────────

        private sealed class Msg91BulkRequest
        {
            [JsonPropertyName("integrated_number")]
            public string IntegratedNumber { get; set; }

            [JsonPropertyName("content_type")]
            public string ContentType { get; set; }

            [JsonPropertyName("payload")]
            public Msg91Payload Payload { get; set; }
        }

        private sealed class Msg91Payload
        {
            [JsonPropertyName("type")]
            public string Type { get; set; }

            [JsonPropertyName("template")]
            public Msg91Template Template { get; set; }

            [JsonPropertyName("messaging_product")]
            public string MessagingProduct { get; set; }
        }

        private sealed class Msg91Template
        {
            [JsonPropertyName("name")]
            public string Name { get; set; }

            [JsonPropertyName("language")]
            public Msg91Language Language { get; set; }

            [JsonPropertyName("to_and_components")]
            public List<Msg91Recipient> ToAndComponents { get; set; }
        }

        private sealed class Msg91Language
        {
            [JsonPropertyName("code")]
            public string Code { get; set; }

            [JsonPropertyName("policy")]
            public string Policy { get; set; } = "deterministic";
        }

        private sealed class Msg91Recipient
        {
            [JsonPropertyName("to")]
            public List<string> To { get; set; }

            [JsonPropertyName("components")]
            public Msg91Components Components { get; set; }
        }

        private sealed class Msg91Components
        {
            [JsonPropertyName("body_1")]
            public Msg91ComponentValue Body1 { get; set; }

            [JsonPropertyName("body_2")]
            public Msg91ComponentValue Body2 { get; set; }
        }

        private sealed class Msg91ComponentValue
        {
            [JsonPropertyName("type")]
            public string Type { get; set; } = "text";

            [JsonPropertyName("value")]
            public string Value { get; set; }
        }
    }
}

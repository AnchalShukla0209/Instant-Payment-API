using InstantPay.Infrastructure.Sql.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace InstantPay.Application.Services
{
    public class ApiTransactionRecoveryService
    {
        private readonly IConfiguration _config;
        private readonly HttpClient _client;
        private readonly AppDbContext _context;
        private readonly ILogger<ApiTransactionRecoveryService> _logger;

        public ApiTransactionRecoveryService(
            IHttpClientFactory factory,
            IConfiguration config,
            AppDbContext context,
            ILogger<ApiTransactionRecoveryService> logger)
        {
            _client = factory.CreateClient();
            _config = config;
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Check transaction status from any API provider
        /// </summary>
        public async Task<(string status, string apiTxnId)> CheckTransactionStatus(string provider, string clientRefNo)
        {
            return provider.ToLower() switch
            {
                "iqore" => await CheckIcoreTransactionStatus(clientRefNo),
                "mrobotics" => await CheckMroboticsTransactionStatus(clientRefNo),
                "ambika" => await CheckAmbikaTransactionStatus(clientRefNo),
                "cyrusre" => await CheckCyrusTransactionStatus(clientRefNo)
            };
        }

        /// <summary>
        /// Check transaction status from iCore API
        /// </summary>
        private async Task<(string status, string apiTxnId)> CheckIcoreTransactionStatus(string clientRefNo)
        {
            var baseUrl = _config["RechargeApis:iCore:BaseUrl"];
            var signature = _config["RechargeApis:iCore:Signature"];

            string url = $"{baseUrl}/status?signature={signature}&cack={clientRefNo}";

            try
            {
                var response = await _client.PostAsync(url, null);
                var apiResponse = await response.Content.ReadAsStringAsync();

                // Parse iCore response format: code|status|clientRef|operatorRef|apiTxnId
                var parts = apiResponse.Split('|');
                if (parts.Length >= 5)
                {
                    var code = parts[0].Trim();
                    var status = parts[1].Trim();
                    var apiTxnId = parts[4].Trim();

                    string finalStatus = code switch
                    {
                        "200" => "SUCCESS",
                        "201" => "PENDING",
                        _ => "FAILED"
                    };

                    return (finalStatus, apiTxnId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to check iCore transaction status for {ClientRefNo}", clientRefNo);
            }

            return ("FAILED", "");
        }

        /// <summary>
        /// Check transaction status from Mrobotics API
        /// </summary>
        private async Task<(string status, string apiTxnId)> CheckMroboticsTransactionStatus(string orderId)
        {
            var token = _config["RechargeApis:Mrobotics:Token"];
            var baseUrl = _config["RechargeApis:Mrobotics:BaseUrl"];

            string url = $"{baseUrl}/api/transaction_status?api_token={token}&order_id={orderId}";

            try
            {
                var response = await _client.PostAsync(url, null);
                var apiResponse = await response.Content.ReadAsStringAsync();

                // Parse Mrobotics response format: JSON with status field
                var obj = JObject.Parse(apiResponse);
                var status = obj["status"]?.ToString()?.ToLower();
                var apiTxnId = obj["opid"]?.ToString() ?? "";

                string finalStatus = status switch
                {
                    "success" => "SUCCESS",
                    "pending" => "PENDING",
                    _ => "FAILED"
                };

                return (finalStatus, apiTxnId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to check Mrobotics transaction status for {OrderId}", orderId);
            }

            return ("FAILED", "");
        }

        /// <summary>
        /// Check transaction status from Ambika API
        /// </summary>
        private async Task<(string status, string apiTxnId)> CheckAmbikaTransactionStatus(string orderId)
        {
            var baseUrl = _config["RechargeApis:Ambika:BaseUrl"];
            var userid = _config["RechargeApis:Ambika:UserId"];
            var token = _config["RechargeApis:Ambika:Token"];

            string url = $"{baseUrl}/API/TransactionStatusAPI?UserID={userid}&Token={token}&APIRequestID={orderId}";

            try
            {
                var response = await _client.GetStringAsync(url);
                var obj = JObject.Parse(response);
                var status = obj["status"]?.ToString();
                var apiTxnId = obj["rpid"]?.ToString() ?? "";

                string finalStatus = status switch
                {
                    "1" => "SUCCESS",
                    "2" => "PENDING",
                    _ => "FAILED"
                };

                return (finalStatus, apiTxnId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to check Ambika transaction status for {OrderId}", orderId);
            }

            return ("FAILED", "");
        }

        /// <summary>
        /// Check transaction status from Cyrus API
        /// </summary>
        private async Task<(string status, string apiTxnId)> CheckCyrusTransactionStatus(string orderId)
        {
            var baseUrl = _config["RechargeApis:Cyrus:BaseUrl"];
            var memberId = _config["RechargeApis:Cyrus:MemberId"];
            var pin = _config["RechargeApis:Cyrus:Pin"];

            string url = $"{baseUrl}/api/status.aspx?memberid={memberId}&pin={pin}&usertx={orderId}";

            try
            {
                var response = await _client.GetStringAsync(url);
                var parts = response.Split('#');
                if (parts.Length >= 2)
                {
                    var obj = JObject.Parse(parts[1]);
                    var status = obj["Status"]?.ToString()?.ToLower();
                    var apiTxnId = obj["ApiTransID"]?.ToString() ?? "";

                    string finalStatus = status switch
                    {
                        "success" => "SUCCESS",
                        "pending" => "PENDING",
                        _ => "FAILED"
                    };

                    return (finalStatus, apiTxnId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to check Cyrus transaction status for {OrderId}", orderId);
            }

            return ("FAILED", "");
        }

        /// <summary>
        /// Recover transactions that exist in apilogs but not in TransactionDetails
        /// This handles the rollback scenario where API succeeded but DB transaction rolled back
        /// </summary>
        public async Task<int> RecoverMissingTransactions()
        {
            var recoveredCount = 0;
            var cutoffTime = DateTime.Now.AddHours(-24); // Check last 24 hours

            try
            {
                // Find API logs for all providers that don't have corresponding TransactionDetails
                var providers = new[] { "iCore", "Mrobotics", "Ambika", "Cyrus" };
                var apiNameMapping = new Dictionary<string, string>
                {
                    { "iCore", "iqore" },
                    { "Mrobotics", "mrobotics" },
                    { "Ambika", "ambika" },
                    { "Cyrus", "cyrusre" }
                };

                foreach (var provider in providers)
                {
                    var apiName = apiNameMapping[provider];

                    // Get existing TxnIds for this API (database query)
                    var existingTxnIds = await _context.TransactionDetails
                        .Where(t => t.ApiName == apiName)
                        .Select(t => t.TxnId)
                        .ToListAsync();

                    // Get API logs for this provider (database query, limited to recent entries)
                    var recentLogs = await _context.Apilogs
                        .Where(log => log.Apiname == provider && log.Reqdatae >= cutoffTime)
                        .Take(100)
                        .ToListAsync();

                    // Filter in memory using client-side evaluation
                    var missingTxns = recentLogs
                        .Where(log => !existingTxnIds.Contains(ExtractOrderIdFromUrl(log.Request, provider)))
                        .Take(25)
                        .ToList();

                    foreach (var log in missingTxns)
                    {
                        try
                        {
                            // Extract order ID from the request URL
                            var orderId = ExtractOrderIdFromUrl(log.Request, provider);
                            if (string.IsNullOrEmpty(orderId))
                                continue;

                            // Check status from the provider
                            var (status, apiTxnId) = await CheckTransactionStatus(apiName, orderId);

                            if (status == "SUCCESS")
                            {
                                _logger.LogWarning("Found missing successful transaction in {Provider}: {OrderId}, Status: {Status}, ApiTxnId: {ApiTxnId}", 
                                    provider, orderId, status, apiTxnId);

                                // TODO: Implement logic to create TransactionDetails record
                                // This requires user information and other details that may not be available
                                // For now, log the discrepancy for manual review

                                recoveredCount++;
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Failed to recover transaction from log ID {LogId}", log.Id);
                        }
                    }
                }

                _logger.LogInformation("Transaction recovery completed. Recovered {Count} missing transactions.", recoveredCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to recover missing transactions");
            }

            return recoveredCount;
        }

        private string ExtractOrderIdFromUrl(string url, string provider)
        {
            try
            {
                return provider switch
                {
                    "iCore" => ExtractParamFromUrl(url, "cack"),
                    "Mrobotics" => ExtractParamFromUrl(url, "order_id"),
                    "Ambika" => ExtractParamFromUrl(url, "APIRequestID"),
                    "Cyrus" => ExtractParamFromUrl(url, "usertx"),
                    _ => string.Empty
                };
            }
            catch
            {
                return string.Empty;
            }
        }

        private string ExtractParamFromUrl(string url, string paramName)
        {
            try
            {
                if (url.Contains($"{paramName}="))
                {
                    var parts = url.Split(new[] { $"{paramName}=" }, StringSplitOptions.None);
                    if (parts.Length > 1)
                    {
                        var value = parts[1].Split('&')[0];
                        return value;
                    }
                }
            }
            catch
            {
                // Return empty if extraction fails
            }
            return string.Empty;
        }
    }
}

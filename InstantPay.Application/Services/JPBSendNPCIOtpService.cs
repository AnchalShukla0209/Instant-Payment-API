using InstantPay.Application.Interfaces;
using InstantPay.Infrastructure.Sql.Entities;
using InstantPay.SharedKernel.RequestPayload;
using InstantPay.SharedKernel.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace InstantPay.Application.Services
{
    public class JPBSendNPCIOtpService : IJPBSendNPCIOtp
    {
        private readonly AppDbContext _context;
        private readonly IHttpClientFactory _httpFactory;
        private readonly IConfiguration _config;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public JPBSendNPCIOtpService(AppDbContext context, IHttpClientFactory httpFactory, IConfiguration config, IHttpContextAccessor httpContextAccessor)
        {
            _context = context;
            _httpFactory = httpFactory;
            _config = config;
            _httpContextAccessor = httpContextAccessor;
        }

        private string? GetRemoteIpAddress()
        {
            try
            {
                var ctx = _httpContextAccessor.HttpContext;
                if (ctx == null) return null;
                var ip = ctx.Connection.RemoteIpAddress;
                return ip?.ToString();
            }
            catch
            {
                return null;
            }
        }

        public async Task<JioOtpResponseDto> SendOtpAsync(JioOtpRequest request, CancellationToken cancellationToken = default)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));

            var cfg = _config.GetSection("JPBAEPS");
            var deviceConfig = cfg.GetSection("DeviceInfo");
            string ipAddress = GetRemoteIpAddress() ?? deviceConfig.GetValue<string>("ipAddressFallback") ?? "0.0.0.0";
            var deviceInfoObj = new
            {
                ipAddress = ipAddress,
                type = deviceConfig.GetValue<string>("type") ?? "MOB",
                os = deviceConfig.GetValue<string>("os") ?? "ANDROID",
                appName = deviceConfig.GetValue<string>("appName") ?? "Jiopay",
                appId = deviceConfig.GetValue<string>("appId") ?? "com.jiobank.aeps",
                sdkVersion = deviceConfig.GetValue<string>("sdkVersion") ?? "1.0.0",
                mobile = string.IsNullOrEmpty(request.Mobile) ? null : request.Mobile,
                userAgent = deviceConfig.GetValue<string>("userAgent") ?? "JioBankSDK/1.0"
            };
            string deviceInfoJson = System.Text.Json.JsonSerializer.Serialize(deviceInfoObj);

            var (otpReferenceId, accessToken, appIdentifierToken) = await GenerateAePSOtpReferenceAsync(request, request.AccessToken, request.AppIdentifierToken, deviceInfoJson);

            bool success = !string.IsNullOrEmpty(otpReferenceId);
            return new JioOtpResponseDto
            {
                Success = success,
                ResponseCode = success ? "00" : "33",
                ResponseMessage = success ? "SUCCESS" : "Aadhaar OTP authentication failed",
                ResponseData = success ? new JioOtpResponseData { OtpReferenceId = otpReferenceId } : null,
                TraceId = Guid.NewGuid().ToString(),
                accessToken = accessToken,
                appIdentifierToken = appIdentifierToken
            };
        }

        private async Task<JioAuthResponse> GenerateSessionToken(string mobile)
        {
            string aesKey = JioAuthHelper.GenerateRandomString(16);
            string secretKey = _config["JPBAEPS:SecretKey"];

            string encryptedValue = JioAuthHelper.EncryptAES(secretKey, aesKey);

            string webRootPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
            string basePath = Path.Combine(webRootPath, "JioPaymentPEMFile");
            string filePath = Path.Combine(basePath, "prod_apigw_publicKey.pem");
            string publicKeyText = File.ReadAllText(filePath);
            string rsaKey = JioAuthHelper.EncryptRSA(aesKey, publicKeyText);

            string url = _config["JPBAEPS:BaseUrl"] + _config["JPBAEPS:AuthSessionPath"];

            var client = _httpFactory.CreateClient("JIO");

            var payload = new
            {
                application = new
                {
                    applicationName = _config["JPBAEPS:ApplicationName"],
                    clientId = _config["JPBAEPS:clientId"]
                },
                authenticateList = new[] { new { mode = 20, value = encryptedValue } },
                purpose = 2,
                scope = _config["JPBAEPS:SessionScope"],
                secure = new { encryptionKey = rsaKey }
            };
            var cfg = _config.GetSection("JPBAEPS");
            var deviceConfig = cfg.GetSection("DeviceInfo");
            string ipAddress = GetRemoteIpAddress() ?? deviceConfig.GetValue<string>("ipAddressFallback") ?? "0.0.0.0";
            var deviceInfoObj = new
            {
                ipAddress = ipAddress,
                type = deviceConfig.GetValue<string>("type") ?? "MOB",
                os = deviceConfig.GetValue<string>("os") ?? "ANDROID",
                appName = deviceConfig.GetValue<string>("appName") ?? "Jiopay",
                appId = deviceConfig.GetValue<string>("appId") ?? "com.jiobank.aeps",
                sdkVersion = deviceConfig.GetValue<string>("sdkVersion") ?? "1.0.0",
                mobile = string.IsNullOrEmpty(mobile) ? null : mobile,
                userAgent = deviceConfig.GetValue<string>("userAgent") ?? "JioBankSDK/1.0"
            };
            string deviceInfoJson = System.Text.Json.JsonSerializer.Serialize(deviceInfoObj);
            var req = new HttpRequestMessage(HttpMethod.Post, url);
            req.Version = HttpVersion.Version11;
            req.VersionPolicy = HttpVersionPolicy.RequestVersionExact;
            req.Content = new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json");
            req.Headers.ExpectContinue = false;
            req.Headers.Add("x-channel-id", _config["JPBAEPS:channelId"]);
            req.Headers.Add("x-trace-id", Guid.NewGuid().ToString());
            req.Headers.Add("x-device-info", deviceInfoJson);

            try
            {
                var response = await client.SendAsync(req);
                string resp = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode) return null;

                dynamic json = JsonConvert.DeserializeObject(resp);

                return new JioAuthResponse
                {
                    AccessToken = json.session.accessToken.tokenValue,
                    RefreshToken = json.session.refreshToken.tokenValue,
                    AppIdentifierToken = json.session.appIdentifierToken
                };
            }
            catch
            {
                return null;
            }
        }

        private async Task<(string OtpReferenceId, string AccessToken, string AppIdentifierToken)> GenerateAePSOtpReferenceAsync(JioOtpRequest model, string accessToken, string appIdToken, string deviceInfoJson)
        {
            string baseUrl = (_config["JPBAEPS:BaseUrl"] ?? "").TrimEnd('/');
            string path = _config["JPBAEPS:OtpGeneratePath"] ?? "/jpb/v1/user/authenticate";
            string url = baseUrl + path;
            var client = _httpFactory.CreateClient("JIO");

            string consentText = "I hereby provide my consent to Jio Payments Bank Limited (\"Bank\") to use my Aadhaar number and biometric authentication to verify my identity for AEPS transactions.";
            string consentBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(consentText));

            var payload = new JObject
            {
                ["user"] = new JObject
                {
                    ["entityType"] = 2,
                    ["userId"] = model.AgentLoginId,
                    ["bankDetails"] = new JObject { ["bankId"] = model.BankId }
                },
                ["scope"] = "REQUEST",
                ["authenticateList"] = new JArray
                {
                    new JObject
                    {
                        ["mode"] = 56,
                        ["action"] = "generate",
                        ["aadhaar"] = new JObject { ["number"] = model.Aadhaar },
                        ["consent"] = consentBase64,
                        ["consentCode"] = "B88"
                    }
                },
                ["purpose"] = 38,
                ["amount"] = (long)model.Amount
            };

            var extraInfo = _config["JPBAEPS:OtpExtraInfo"];
            if (!string.IsNullOrEmpty(extraInfo))
                payload["extraInfo"] = extraInfo;

            string payloadJson = JsonConvert.SerializeObject(payload);

            int attempts = 0;
            const int maxAttempts = 2;

            while (attempts < maxAttempts)
            {
                attempts++;
                string traceId = Guid.NewGuid().ToString();

                using var req = new HttpRequestMessage(HttpMethod.Post, url);
                req.Version = HttpVersion.Version11;
                req.VersionPolicy = HttpVersionPolicy.RequestVersionExact;
                req.Content = new StringContent(payloadJson, Encoding.UTF8, "application/json");
                req.Headers.ExpectContinue = false;
                req.Headers.Add("x-channel-id", _config["JPBAEPS:channelId"]);
                req.Headers.Add("x-trace-id", traceId);
                req.Headers.Add("x-device-info", deviceInfoJson);
                if (!string.IsNullOrEmpty(appIdToken)) req.Headers.Add("x-appid-token", appIdToken);
                if (!string.IsNullOrEmpty(accessToken)) req.Headers.Add("x-app-access-token", accessToken);
                var clientId = _config["JPBAEPS:clientId"] ?? "";
                if (!string.IsNullOrEmpty(clientId)) req.Headers.Add("clientId", clientId);

                try
                {
                    await LogApiAsync(url, "POST", "INIT", "", deviceInfoJson, payloadJson, null, "AEPS", "GenerateOTP");
                    var response = await client.SendAsync(req);
                    string responseContent = await response.Content.ReadAsStringAsync();
                    await LogApiAsync(url, "POST", response.IsSuccessStatusCode ? "00" : response.StatusCode.ToString(), null,
                        deviceInfoJson + " x-appid-token:" + appIdToken + ",x-app-access-token:" + accessToken + ", traceid:" + traceId,
                        payloadJson, responseContent, "AEPS", "GenerateOTP");

                    if (response.IsSuccessStatusCode)
                    {
                        dynamic json = JsonConvert.DeserializeObject(responseContent);
                        string otpReferenceId = json?.otpReferenceId?.ToString();
                        if (!string.IsNullOrEmpty(otpReferenceId))
                            return (otpReferenceId, accessToken, appIdToken);
                    }

                    bool isSessionExpired = false;

                    try
                    {
                        using var doc = JsonDocument.Parse(responseContent);
                        if (doc.RootElement.TryGetProperty("code", out var codeEl))
                        {
                            var codeString = codeEl.GetRawText().Trim('"');
                            if (codeString == "33306") isSessionExpired = true;
                        }
                        if (!isSessionExpired && doc.RootElement.TryGetProperty("message", out var msgEl))
                        {
                            var msg = msgEl.GetString();
                            if (!string.IsNullOrEmpty(msg) && msg.IndexOf("Invalid Session", StringComparison.OrdinalIgnoreCase) >= 0)
                                isSessionExpired = true;
                        }
                        if (!isSessionExpired)
                        {
                            if (doc.RootElement.TryGetProperty("error", out var errEl) && errEl.TryGetProperty("code", out var errCodeEl))
                            {
                                var ecode = errCodeEl.GetString();
                                if (ecode == "33306" || ecode == "2369") isSessionExpired = true;
                            }
                        }
                    }
                    catch
                    {
                        if (!string.IsNullOrEmpty(responseContent) && responseContent.Contains("33306")) isSessionExpired = true;
                        if (!string.IsNullOrEmpty(responseContent) && responseContent.IndexOf("Invalid Session", StringComparison.OrdinalIgnoreCase) >= 0) isSessionExpired = true;
                    }

                    if (isSessionExpired && attempts < maxAttempts)
                    {
                        var fallbackTokens = await GenerateSessionToken(model.Mobile);
                        if (fallbackTokens != null)
                        {
                            accessToken = fallbackTokens.AccessToken;
                            appIdToken = fallbackTokens.AppIdentifierToken;
                            continue;
                        }
                    }

                    return (null, accessToken, appIdToken);
                }
                catch
                {
                    return (null, accessToken, appIdToken);
                }
            }

            return (null, accessToken, appIdToken);
        }

        private static string Truncate(string input, int maxLen)
        {
            if (string.IsNullOrEmpty(input)) return string.Empty;
            return input.Length <= maxLen ? input : input.Substring(0, maxLen);
        }

        private static string RedactSensitive(string input)
        {
            if (string.IsNullOrEmpty(input)) return string.Empty;
            try
            {
                var redacted = System.Text.RegularExpressions.Regex.Replace(input, @"\d{12,}", "[REDACTED]");
                redacted = System.Text.RegularExpressions.Regex.Replace(redacted, @"[A-Za-z0-9+/]{40,}=*", "[REDACTED]");
                return redacted;
            }
            catch
            {
                return "[REDACTED]";
            }
        }

        private async Task LogApiAsync(string url, string method, string successCode, string apiError, string headersJson, string payloadJson, string responseJson, string service, string mode)
        {
            try
            {
                var log = new JioPaymentAPILog
                {
                    APIURL = Truncate(url, 200),
                    Method = Truncate(method, 100),
                    SuccessCode = Truncate(successCode, 10),
                    APIError = apiError,
                    APIHeaders = headersJson,
                    APIPayload = payloadJson,
                    APIResponse = responseJson,
                    CreatedOn = DateTime.Now,
                    Service = service,
                    Mode = Truncate(mode, 100)
                };

                await _context.JioPaymentAPILogs.AddAsync(log);
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }
    }
}

using InstantPay.Application.Interfaces;
using InstantPay.Infrastructure.Sql.Entities;
using InstantPay.SharedKernel.RequestPayload;
using InstantPay.SharedKernel.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;

namespace InstantPay.Application.Services
{
    public class AEPSService : IAEPSService
    {
        private readonly AppDbContext _context;
        private readonly IHttpClientFactory _httpFactory;
        private readonly IConfiguration _config;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly TimeSpan _cacheDuration;
        public AEPSService(AppDbContext context, IHttpClientFactory httpFactory, IConfiguration config, IHttpContextAccessor httpContextAccessor)
        {
            _context = context;
            _httpFactory = httpFactory;
            _config = config;
            _httpContextAccessor = httpContextAccessor;
            var hours = _config.GetSection("JPBAEPS").GetValue<int?>("CacheHours") ?? 24;
            _cacheDuration = TimeSpan.FromHours(hours);
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
            catch (Exception ex)
            {
                return null;
            }
        }


        public async Task<JIOAgentLoginResponse> GetJioAgentAEPSLogin(string agentId, string mobile, string AadharNumber)
        {
            agentId = agentId?.Trim() ?? string.Empty;
            mobile = mobile?.Trim() ?? string.Empty;

            if (string.IsNullOrEmpty(agentId))
            {
                return new JIOAgentLoginResponse { success = false, message = "AgentId is required", StatusCode = "99" };
            }
            var cutoff = DateTime.Now.Subtract(_cacheDuration);
            var sessionResponse = await GenerateSessionToken(mobile);
            var todayEntry = await _context.JPAgentDailyLogins
                .Where(x => x.AgentLoginId == agentId && x.Mobile == mobile && x.AadharNumber == AadharNumber && x.LoginDateTime >= cutoff)
                .OrderByDescending(x => x.LoginDateTime)
                .FirstOrDefaultAsync();

            if (todayEntry != null /*&& (DateTime.Now - todayEntry.LoginDateTime).TotalHours <= 24*/)
            {
                return new JIOAgentLoginResponse
                {
                    success = true,
                    message = "Cached success",
                    AgentLoginId = todayEntry.AgentLoginId,
                    AgentPinCode = todayEntry.AgentPinCode,
                    Lattitude = todayEntry.Lattitude,
                    Longtitude = todayEntry.Longtitude,
                    StatusCode = "00",
                    aepsauthtoken = sessionResponse.AccessToken,
                    agentrefno = todayEntry.AgentRefNo,
                    appidentifiertoken = sessionResponse.AppIdentifierToken
                };
            }


            if (sessionResponse == null)
            {
                return new JIOAgentLoginResponse { success = false, message = "Session Error", StatusCode = "11" };
            }

            string accessToken = sessionResponse.AccessToken;
            string appIdToken = sessionResponse.AppIdentifierToken;

            string url = _config["JPBAEPS:BaseUrl"] + _config["JPBAEPS:AgentInfoPath"] + "/" + agentId + "?organizationName=" + _config["JPBAEPS:channelId"];
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


            var client = _httpFactory.CreateClient("JIO");

            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("Accept", "application/json");
            request.Headers.Add("x-channel-id", _config["JPBAEPS:channelId"]);
            request.Headers.Add("x-appid-token", appIdToken);
            request.Headers.Add("x-app-access-token", accessToken);
            request.Headers.Add("x-trace-id", Guid.NewGuid().ToString());
            request.Headers.Add("x-device-info", deviceInfoJson);
            request.Headers.Add("clientId", _config["JPBAEPS:clientId"]);

            string apiResponse = "";
            string apiError = "";

            try
            {
                var response = await client.SendAsync(request);
                apiResponse = await response.Content.ReadAsStringAsync();
                dynamic json = JsonConvert.DeserializeObject(apiResponse);
                if (!response.IsSuccessStatusCode)
                {
                    await LogAPI(url, "GET", "11", apiError, "", "", apiResponse, "AEPS", "AgentInfo");
                    return new JIOAgentLoginResponse { success = false, message = json.message ?? "API Error", StatusCode = "11" };
                }



                if (json.message != null && json.message.ToString().Contains("does not exist"))
                {
                    await LogAPI(url, "GET", "22", "", "", "", apiResponse, "AEPS", "AgentInfo");
                    return new JIOAgentLoginResponse { success = false, message = "Agent not found", StatusCode = "22" };
                }

                string agentLoginId = json.loginId;
                string pincode = json.agent.pinCode;
                double lat = json.agent.latitude;
                double lon = json.agent.longitude;
                string agentrefno = json.externalUserId;

                var save = new JPAgentDailyLogin
                {
                    AgentLoginId = agentLoginId,
                    AgentPinCode = pincode,
                    Lattitude = lat,
                    Longtitude = lon,
                    Status = true,
                    LoginDateTime = DateTime.Now,
                    Mobile = mobile,
                    AadharNumber = AadharNumber,
                    AgentRefNo = agentrefno
                };

                _context.JPAgentDailyLogins.Add(save);
                await _context.SaveChangesAsync();

                await LogAPI(url, "GET", "00", "", "", "", apiResponse, "AEPS", "AgentInfo");

                return new JIOAgentLoginResponse
                {
                    success = true,
                    message = "Success",
                    StatusCode = "00",
                    AgentLoginId = agentLoginId,
                    AgentPinCode = pincode,
                    Lattitude = lat,
                    Longtitude = lon,
                    aepsauthtoken = accessToken,
                    agentrefno = agentrefno,
                    appidentifiertoken = sessionResponse.AppIdentifierToken
                };
            }
            catch (Exception ex)
            {
                apiError = ex.Message;
                await LogAPI(url, "GET", "33", apiError, "", "", apiResponse, "AEPS", "AgentInfo");
                return new JIOAgentLoginResponse { success = false, message = "Exception", StatusCode = "33" };
            }
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
            req.Content = new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json");

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

        private async Task LogAPI(string url, string method, string code, string error, string headers, string payload, string response, string service, string mode)
        {
            var log = new JioPaymentAPILog
            {
                APIURL = url,
                Method = method,
                SuccessCode = code,
                APIError = error,
                APIHeaders = headers,
                APIPayload = payload,
                APIResponse = response,
                CreatedOn = DateTime.Now,
                IsDeleted = false,
                Service = service,
                Mode = mode
            };

            _context.JioPaymentAPILogs.Add(log);
            await _context.SaveChangesAsync();
        }

        public async Task<JIOAgentLoginResponse> CheckAgentDailyLoginAsync(int UserId)
        {
            var UserData = _context.TblUsers.Where(id => id.Id == UserId).FirstOrDefault();
            if (UserData == null)
            {
                return new JIOAgentLoginResponse
                {
                    success = false,
                    message = "Invalid User Id",
                    StatusCode = "401",
                    transactiondatetime = DateTime.Now.ToString("o")
                };
            }
            string mobile = UserData?.Phone.Trim();
            string AadharNo = UserData?.AadharCard.Trim();
            if (string.IsNullOrWhiteSpace(mobile))
            {
                return new JIOAgentLoginResponse
                {
                    success = false,
                    message = "Mobile No Required",
                    StatusCode = "33",
                    transactiondatetime = DateTime.Now.ToString("o"),
                };
            }

            try
            {
                //var cutoff = DateTime.Now.Subtract(_cacheDuration);

                //var cutoff = DateTime.Now.AddHours(-24);

                var cutoff = DateTime.Now.AddHours(-24);

                var todayEntry = await _context.JPAgentDailyLogins
                    .Where(x =>
                        x.Mobile == mobile &&
                        x.AadharNumber == AadharNo &&
                        x.LoginDateTime >= cutoff
                    )
                    .OrderByDescending(x => x.LoginDateTime)
                    .FirstOrDefaultAsync();



                var sessionResponse = await GenerateSessionToken(mobile);
                if (todayEntry == null)
                {
                    return new JIOAgentLoginResponse
                    {
                        success = false,
                        message = "Agent not logged today",
                        StatusCode = "11",
                        transactiondatetime = DateTime.Now.ToString("o"),
                        aepsauthtoken = sessionResponse.AccessToken,
                        appidentifiertoken = sessionResponse.AppIdentifierToken,
                    };
                }

                return new JIOAgentLoginResponse
                {
                    success = true,
                    message = "Agent already logged in",
                    txnid = "00",
                    apitxnid = todayEntry.Id.ToString(),
                    transactiondatetime = todayEntry.LoginDateTime.ToString("o"),
                    AgentLoginId = todayEntry.AgentLoginId,
                    AgentPinCode = todayEntry.AgentPinCode,
                    Lattitude = todayEntry.Lattitude,
                    Longtitude = todayEntry.Longtitude,
                    StatusCode = "00",
                    aepsauthtoken = sessionResponse.AccessToken,
                    agentrefno = todayEntry.AgentRefNo,
                    appidentifiertoken = sessionResponse.AppIdentifierToken
                };
            }
            catch (Exception ex)
            {

                return new JIOAgentLoginResponse
                {
                    success = false,
                    message = "Internal error",
                    StatusCode = "33",
                    transactiondatetime = DateTime.Now.ToString("o"),
                };
            }
        }


        public async Task<CreateAgentResponseDto> CreateAgentAsync(CreateAgentRequestDto model, CancellationToken cancellationToken = default)
        {
            if (model == null) throw new ArgumentNullException(nameof(model));

            if (string.IsNullOrWhiteSpace(model.RefNo) ||
                string.IsNullOrWhiteSpace(model.PAN) ||
                string.IsNullOrWhiteSpace(model.Mobile))
            {
                return new CreateAgentResponseDto { Success = false, Message = "RefNo, PAN and Mobile are required", StatusCode = "33" };
            }

            var cfg = _config.GetSection("JPBAEPS");
            string channelId = cfg.GetValue<string>("channelId") ?? "7215";
            string apiBase = (cfg.GetValue<string>("BaseUrl") ?? "").TrimEnd('/');
            string apiPath = cfg.GetValue<string>("AgentCreatePath") ?? "/jpb/exp/app-mgmt/partner/api/onboarding";
            string apiUrl = apiBase + apiPath;

            string accessToken = model.AccessToken;
            string appIdToken = model.AppIdentifierToken;

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
                mobile = model.Mobile,
                userAgent = deviceConfig.GetValue<string>("userAgent") ?? "JioBankSDK/1.0"
            };
            string deviceInfoJson = System.Text.Json.JsonSerializer.Serialize(deviceInfoObj);

            var payload = new
            {
                externalAppRefNumber = model.RefNo,
                apiVersion = "1.0",
                applicationType = "Partner",
                applicationSubType = "Onboarding",
                initiatingEntityId = channelId,
                app = "APIPARTNER",
                action = new { type = "Creation", subType = "Save" },
                organization = new { id = channelId },
                persons = new object[]
                {
                new {
                    personType = "INDIVIDUAL",
                    financialDetails = new { panNumber = model.PAN },
                    externalId = model.RefNo,
                    address = new object[] {
                        new {
                            addressType = "WORK",
                            careOf = "S/o",
                            houseNumber = model.Address,
                            street = "NA",
                            city = model.City,
                            district = model.City,
                            state = model.State,
                            stateCode = (model.State ?? "").Length >= 2 ? (model.State ?? "").Substring(0,2).ToUpper() : model.State,
                            country = "India",
                            pincode = model.Pincode,
                            geoLocation = new { latitude = "27.5626", longitude = "80.6826" }
                        }
                    },
                    contactDetails = new object[] {
                        new { type = "Mobile", countryCode = "+91", mobileNumber = model.Mobile, status = "PreVerified" },
                        new { type = "Personal Email", email = model.Email }
                    }
                }
                },
                products = new object[]
                {
                new {
                    productType = "AGENT",
                    paymentInstruments = new object[]
                    {
                        new { id = "DMR", enabled = true },
                        new { id = "AEPS", enabled = true }
                    }
                }
                }
            };

            string requestBody = JsonConvert.SerializeObject(payload);

            var client = _httpFactory.CreateClient("JIO");

            int attempts = 0;
            const int maxAttempts = 2;
            JioAuthResponse fallbackTokens = null;

            while (attempts < maxAttempts)
            {
                attempts++;

                using var req = new HttpRequestMessage(HttpMethod.Post, apiUrl);
                req.Content = new StringContent(requestBody, Encoding.UTF8, "application/json");

                // headers
                req.Headers.Add("x-channel-id", channelId);
                if (!string.IsNullOrEmpty(appIdToken)) req.Headers.Add("x-appid-token", appIdToken);
                if (!string.IsNullOrEmpty(accessToken)) req.Headers.Add("x-app-access-token", accessToken);
                req.Headers.Add("x-trace-id", Guid.NewGuid().ToString());
                req.Headers.Add("x-device-info", deviceInfoJson);
                req.Headers.Add("clientId", cfg.GetValue<string>("clientId") ?? "");

                HttpResponseMessage resp = null;
                string responseContent = null;

                try
                {
                    resp = await client.SendAsync(req, cancellationToken);
                    responseContent = await resp.Content.ReadAsStringAsync(cancellationToken);

                    // log
                    await LogApiAsync(apiUrl, "POST", resp.IsSuccessStatusCode ? "00" : resp.StatusCode.ToString(), null, deviceInfoJson, requestBody, responseContent, "AEPS", "AgentCreate");

                    // if success -> break and process
                    if (resp.IsSuccessStatusCode)
                    {
                        dynamic resultObj = JsonConvert.DeserializeObject(responseContent);

                        // check expected success shape, prefer resultObj.data.applicationNumber
                        string applicationNumber = resultObj?.data?.applicationNumber ?? string.Empty;
                        string status = resultObj?.status ?? (resultObj?.data?.status ?? string.Empty);
                        var msg = "";

                        msg = status == "FAILED"
                          ? resultObj?.error?.message
                          : "Agent Created Successfully, Please Complete EKYC.";

                        // Persist AgentInfoJPB
                        try
                        {
                            var agentRow = new AgentInfoJPB
                            {
                                ApplicationNumber = applicationNumber,
                                AgentRefNo = model.RefNo,
                                Status = status ?? "UNKNOWN",
                                UserId = model.UserId ?? 0,
                                UserType = model.UserType ?? string.Empty,
                                isActive = true,
                                CreatedOn = DateTime.Now
                            };

                            await _context.AgentInfoJPBs.AddAsync(agentRow, cancellationToken);
                            await _context.SaveChangesAsync(cancellationToken);
                        }
                        catch (Exception dbEx)
                        {
                            return new CreateAgentResponseDto
                            {
                                Success = true,
                                Message = "Agent created (DB save failed)",
                                StatusCode = "00",
                                ApplicationNumber = resultObj?.data?.applicationNumber ?? "",
                                AgentRefNo = model.RefNo,
                                Status = resultObj?.status ?? resultObj?.data?.status ?? ""
                            };
                        }

                        return new CreateAgentResponseDto
                        {
                            Success = status == "FAILED" ? false : true,
                            Message = msg,
                            StatusCode = "00",
                            ApplicationNumber = applicationNumber,
                            AgentRefNo = model.RefNo,
                            Status = status ?? "",
                            AccessToken = accessToken,
                            AppIdentifierToken = appIdToken
                        };
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
                        // ignore parse errors; fallback to string search
                        if (!isSessionExpired && !string.IsNullOrEmpty(responseContent) && responseContent.Contains("33306")) isSessionExpired = true;
                        if (!isSessionExpired && !string.IsNullOrEmpty(responseContent) && responseContent.IndexOf("Invalid Session", StringComparison.OrdinalIgnoreCase) >= 0) isSessionExpired = true;
                    }

                    if (isSessionExpired && attempts < maxAttempts)
                    {
                        // fallback generate new tokens
                        fallbackTokens = await GenerateSessionToken(model.Mobile);
                        if (fallbackTokens == null)
                        {
                            return new CreateAgentResponseDto { Success = false, Message = "Session expired and refresh failed", StatusCode = "22" };
                        }

                        // replace tokens and retry
                        accessToken = fallbackTokens.AccessToken;
                        appIdToken = fallbackTokens.AppIdentifierToken;
                        continue; // next attempt
                    }
                    return new CreateAgentResponseDto
                    {
                        Success = false,
                        Message = "Agent create API error",
                        StatusCode = resp.StatusCode.ToString()
                    };
                }
                catch (OperationCanceledException oce) when (cancellationToken.IsCancellationRequested)
                {
                    return new CreateAgentResponseDto { Success = false, Message = "Request canceled", StatusCode = "33" };
                }
                catch (Exception ex)
                {
                    await LogApiAsync(apiUrl, "POST", "33", ex.Message, deviceInfoJson, requestBody, responseContent, "AEPS", "AgentCreate");
                    return new CreateAgentResponseDto { Success = false, Message = "Unexpected error", StatusCode = "33" };
                }
            } // end while

            // If we exit loop unexpectedly
            return new CreateAgentResponseDto { Success = false, Message = "Unhandled flow", StatusCode = "33" };
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
                    SuccessCode = successCode,
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

        public async Task<AgentEKYCResponseDto> AgentEKYCAsync(AgentEKYCRequestDto model, CancellationToken cancellationToken = default)
        {
            if (model == null) throw new ArgumentNullException(nameof(model));
            if (string.IsNullOrWhiteSpace(model.AadhaarValue))
                return new AgentEKYCResponseDto { Success = false, Message = "Aadhaar is required", StatusCode = "33" };

            var cfg = _config.GetSection("JPBAEPS");
            string channelId = cfg.GetValue<string>("channelId") ?? "7215";
            string apiBase = (cfg.GetValue<string>("BaseUrl") ?? "").TrimEnd('/');
            string apiPath = cfg.GetValue<string>("AgentEKYCPath") ?? "/jpb/exp/app-mgmt/partner/api/ekyc";
            string apiUrl = apiBase + apiPath;

            string accessToken = model.AccessToken;
            string appIdToken = model.AppIdentifierToken;

            // device info
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
                mobile = model.Mobile,
                userAgent = deviceConfig.GetValue<string>("userAgent") ?? "JioBankSDK/1.0"
            };
            string deviceInfoJson = System.Text.Json.JsonSerializer.Serialize(deviceInfoObj);

            // build pidBase64 (prefer model.PidBase64 else convert PidXml)
            string pidBase64 = "";
            if (string.IsNullOrWhiteSpace(model.PidXml))
                return new AgentEKYCResponseDto { Success = false, Message = "PID XML required", StatusCode = "33", appIdentifierToken = appIdToken, accessToken = accessToken };
            try
            {
                pidBase64 = ConvertPidXmlToBase64(model.PidXml);
            }
            catch (Exception ex)
            {
                return new AgentEKYCResponseDto { Success = false, Message = "finger print data is missing", StatusCode = "33", appIdentifierToken = appIdToken, accessToken = accessToken };
            }

            var payload = new
            {
                applicationNumber = model.ApplicationNumber,
                apiVersion = "1.0",
                applicationType = "Partner",
                applicationSubType = "Onboarding",
                initiatingEntityId = string.IsNullOrWhiteSpace(model.InitiatingEntityId) ? channelId : model.InitiatingEntityId,
                app = "APIPARTNER",
                action = new { type = "AADHAAR", subType = "EKYC-BIOMETRIC" },
                authenticateList = new object[]
                {
                    new {
                        aadhaar = new { type = "UID", value = model.AadhaarValue.Trim() },
                        value = pidBase64
                    }
                },
                consents = new object[]
                {
                    new {
                        consent = "I hereby provide my consent to Jio Payments Bank Limited (Bank) to use my Aadhaar number and biometric authentication to verify my identity for the purpose of doing eKYC...",
                        code = "B88",
                        version = "1",
                        method = "checkbox"
                    }
                }
            };

            string requestBody = JsonConvert.SerializeObject(payload);
            var client = _httpFactory.CreateClient("JIO");

            int attempts = 0;
            const int maxAttempts = 2;
            JioAuthResponse fallbackTokens = null;

            while (attempts < maxAttempts)
            {
                attempts++;
                using var req = new HttpRequestMessage(HttpMethod.Post, apiUrl);
                req.Content = new StringContent(requestBody, Encoding.UTF8, "application/json");

                // headers
                req.Headers.Add("x-channel-id", channelId);
                if (!string.IsNullOrEmpty(appIdToken)) req.Headers.Add("x-appid-token", appIdToken);
                if (!string.IsNullOrEmpty(accessToken)) req.Headers.Add("x-app-access-token", accessToken);
                req.Headers.Add("x-trace-id", Guid.NewGuid().ToString());
                req.Headers.Add("x-device-info", deviceInfoJson);
                req.Headers.Add("clientId", _config["JPBAEPS:clientId"]);

                HttpResponseMessage resp = null;
                string responseContent = null;
                try
                {
                    resp = await client.SendAsync(req, cancellationToken);
                    responseContent = await resp.Content.ReadAsStringAsync(cancellationToken);

                    // log
                    await LogApiAsync(apiUrl, "POST", resp.IsSuccessStatusCode ? "00" : resp.StatusCode.ToString(), null, deviceInfoJson, requestBody, responseContent, "AEPS", "AgentEKYC");

                    if (resp.IsSuccessStatusCode)
                    {
                        // parse minimal fields
                        string externalAppRef = null;
                        string appNumber = null;
                        string status = null;
                        string message = null;
                        string errCode = null;
                        string errMsg = null;

                        try
                        {
                            dynamic resultObj = JsonConvert.DeserializeObject(responseContent);
                            externalAppRef = resultObj?.externalAppRefNumber ?? resultObj?.externalAppRef ?? null;
                            appNumber = resultObj?.applicationNumber ?? null;
                            status = resultObj?.status ?? (resultObj?.data?.status ?? null);
                            message = resultObj?.message ?? null;
                            if (resultObj?.error != null)
                            {
                                errCode = resultObj.error.code ?? resultObj.error.Code;
                                errMsg = resultObj.error.message ?? resultObj.error.Message;
                            }
                        }
                        catch
                        {
                            // ignore
                        }

                        return new AgentEKYCResponseDto
                        {
                            Success = true,
                            Message = "OK",
                            StatusCode = "00",
                            RawResponse = responseContent,
                            ExternalAppRef = externalAppRef,
                            ApplicationNumber = appNumber,
                            Status = status,
                            ErrorCode = errCode,
                            ErrorMessage = errMsg,
                            appIdentifierToken = appIdToken,
                            accessToken = accessToken
                        };
                    }

                    // detect session expired similar to CreateAgent
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
                        if (!string.IsNullOrEmpty(responseContent) && responseContent.Contains("33306"))
                            isSessionExpired = true;
                        if (!string.IsNullOrEmpty(responseContent) && responseContent.IndexOf("Invalid Session", StringComparison.OrdinalIgnoreCase) >= 0)
                            isSessionExpired = true;
                    }

                    if (isSessionExpired && attempts < maxAttempts)
                    {
                        // try to refresh tokens and retry once
                        fallbackTokens = await GenerateSessionToken(model.Mobile);
                        if (fallbackTokens == null)
                        {
                            return new AgentEKYCResponseDto { Success = false, Message = "Session expired and refresh failed", StatusCode = "22", RawResponse = responseContent, appIdentifierToken = appIdToken, accessToken = accessToken };
                        }

                        accessToken = fallbackTokens.AccessToken;
                        appIdToken = fallbackTokens.AppIdentifierToken;
                        continue; // retry
                    }

                    return new AgentEKYCResponseDto
                    {
                        Success = false,
                        Message = "eKYC API error",
                        StatusCode = resp.StatusCode.ToString(),
                        RawResponse = responseContent,
                        appIdentifierToken = appIdToken,
                        accessToken = accessToken

                    };
                }
                catch (OperationCanceledException oce) when (cancellationToken.IsCancellationRequested)
                {
                    return new AgentEKYCResponseDto { Success = false, Message = "Request canceled", StatusCode = "33" , appIdentifierToken = appIdToken, accessToken = accessToken };
                }
                catch (Exception ex)
                {
                    await LogApiAsync(apiUrl, "POST", "33", ex.Message, deviceInfoJson, requestBody, responseContent, "AEPS", "AgentEKYC");
                    return new AgentEKYCResponseDto { Success = false, Message = "Unexpected error: " + ex.Message, StatusCode = "33", RawResponse = responseContent,  appIdentifierToken = appIdToken, accessToken = accessToken };
                }
            } // end while

            return new AgentEKYCResponseDto { Success = false, Message = "Unhandled flow", StatusCode = "33", appIdentifierToken = appIdToken, accessToken = accessToken };
        }


        public static string ConvertPidXmlToBase64(string pidXml)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(pidXml))
                    throw new ArgumentException("PID XML input is empty.");

                pidXml = pidXml.Trim();

                var doc = XDocument.Parse(pidXml);

                var resp = doc.Root.Element("Resp");
                var deviceInfo = doc.Root.Element("DeviceInfo");
                var skey = doc.Root.Element("Skey");
                var hmac = doc.Root.Element("Hmac");
                var data = doc.Root.Element("Data");

                if (resp == null || deviceInfo == null || skey == null || hmac == null || data == null)
                    throw new Exception("Invalid PID XML structure. Missing required elements.");

                // Extract required biometric elements
                string ci = (string)skey.Attribute("ci") ?? "";
                string sessionKey = skey.Value;
                string hmacValue = hmac.Value;
                string dataValue = data.Value;

                // Step 1️⃣: Create biometric fields JSON
                var bioJson = new
                {
                    Skey = sessionKey,
                    Hmac = hmacValue,
                    Data = dataValue
                };

                string bioJsonString = JsonConvert.SerializeObject(bioJson, Formatting.None);
                string pidDataBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(bioJsonString));

                // Step 2️⃣: Capture response attributes
                string errCode = (string)resp.Attribute("errCode") ?? "";
                string errInfo = (string)resp.Attribute("errInfo") ?? "";
                string fCount = (string)resp.Attribute("fCount") ?? "";
                string fType = (string)resp.Attribute("fType") ?? "";
                string qScore = (string)resp.Attribute("qScore") ?? "";
                string rc = (string)resp.Attribute("rc") ?? "";
                string ver = (string)resp.Attribute("ver") ?? "2.0";

                string dc = (string)deviceInfo.Attribute("dc") ?? "";
                string dpId = (string)deviceInfo.Attribute("dpId") ?? (string)deviceInfo.Attribute("dpID") ?? "";
                string mc = (string)deviceInfo.Attribute("mc") ?? "";
                string mi = (string)deviceInfo.Attribute("mi") ?? "";
                string rdsId = (string)deviceInfo.Attribute("rdsId") ?? (string)deviceInfo.Attribute("rdsID") ?? "";
                string rdsVer = (string)deviceInfo.Attribute("rdsVer") ?? "";

                // Step 3️⃣: Construct final payload
                var payload = new
                {
                    type = "1",
                    captureResponse = new
                    {
                        PidDatatype = "X",
                        Piddata = dataValue,  // Base64 of biometric fields JSON
                        appCode = "BCAPP",
                        ci = ci,
                        consent = Convert.ToBase64String(Encoding.UTF8.GetBytes("I hereby provide my consent for Aadhaar eKYC authentication.")),
                        dc = dc,
                        dpId = dpId,
                        errCode = errCode,
                        errInfo = errInfo,
                        fCount = fCount,
                        fType = fType,
                        hmac = hmacValue,
                        iType = "",
                        mc = mc,
                        mi = mi,
                        nmPoints = "40",
                        pType = "",
                        qScore = qScore,
                        rc = rc,
                        rdsId = rdsId,
                        rdsVer = rdsVer,
                        sa = "SKJIOPAYB1",
                        saTxn = $"BCAPP:{DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture)}",
                        sessionKey = sessionKey,
                        tid = "registered",
                        ver = ver
                    },
                    deviceInfo = new
                    {
                        dc = dc,
                        dpId = dpId,
                        mc = mc,
                        mi = mi,
                        rdsId = rdsId,
                        rdsVer = rdsVer
                    }
                };

                // Step 4️⃣: Convert to Base64 for final API
                string json = JsonConvert.SerializeObject(payload, Formatting.None);
                string base64Json = Convert.ToBase64String(Encoding.UTF8.GetBytes(json));

                return base64Json;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"PID conversion failed: {ex.Message}", ex);
            }
        }

    }

}

public class JioAuthResponse
{
    public string AccessToken { get; set; }
    public string RefreshToken { get; set; }
    public string AppIdentifierToken { get; set; }
}


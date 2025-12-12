using InstantPay.Application.Interfaces;
using InstantPay.Infrastructure.Sql.Entities;
using InstantPay.SharedKernel.RequestPayload;
using InstantPay.SharedKernel.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace InstantPay.Application.Services
{
    public class JPBBalanceEnquiry : IJPBBalanceEnquiry
    {
        private readonly AppDbContext _context;
        private readonly IHttpClientFactory _httpFactory;
        private readonly IConfiguration _config;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly TimeSpan _cacheDuration;
        private readonly IMemoryCache _cache;
        public JPBBalanceEnquiry(AppDbContext context, IHttpClientFactory httpFactory, IConfiguration config, IHttpContextAccessor httpContextAccessor, IMemoryCache cache)
        {
            _context = context;
            _httpFactory = httpFactory;
            _config = config;
            _httpContextAccessor = httpContextAccessor;
            var hours = _config.GetSection("JPBAEPS").GetValue<int?>("CacheHours") ?? 24;
            _cacheDuration = TimeSpan.FromHours(hours);
            _cache = cache;
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

        private static string ConvertPidXmlToBase64(string pidXml)
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

            string ci = (string)skey.Attribute("ci") ?? "";
            string sessionKey = skey.Value;
            string hmacValue = hmac.Value;
            string dataValue = data.Value;

            var bioJson = new
            {
                Skey = sessionKey,
                Hmac = hmacValue,
                Data = dataValue
            };

            string bioJsonString = JsonConvert.SerializeObject(bioJson);
            string pidDataBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(bioJsonString));

            string errCode = (string)resp.Attribute("errCode") ?? "";
            string errInfo = (string)resp.Attribute("errInfo") ?? "success";
            string fCount = (string)resp.Attribute("fCount") ?? "";
            string fType = (string)resp.Attribute("fType") ?? "";
            string qScore = (string)resp.Attribute("qScore") ?? "";
            string rc = (string)resp.Attribute("rc") ?? "Y";
            string ver = (string)resp.Attribute("ver") ?? "2.0";

            string dc = (string)deviceInfo.Attribute("dc") ?? "";
            string dpId = (string)deviceInfo.Attribute("dpId") ?? (string)deviceInfo.Attribute("dpID") ?? "";
            string mc = (string)deviceInfo.Attribute("mc") ?? "";
            string mi = (string)deviceInfo.Attribute("mi") ?? "";
            string rdsId = (string)deviceInfo.Attribute("rdsId") ?? (string)deviceInfo.Attribute("rdsID") ?? "";
            string rdsVer = (string)deviceInfo.Attribute("rdsVer") ?? "";

            var payload = new
            {
                type = "1",
                captureResponse = new
                {
                    PidDatatype = "X",
                    Piddata = dataValue,
                    appCode = "BCAPP",
                    ci = ci,
                    consent = Convert.ToBase64String(Encoding.UTF8.GetBytes("I hereby provide my consent for Aadhaar eKYC authentication.")),
                    dc = dc,
                    dpID = dpId,
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
                    rdsID = rdsId,
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

            string json = JsonConvert.SerializeObject(payload, Formatting.None);
            string base64Json = Convert.ToBase64String(Encoding.UTF8.GetBytes(json));
            return base64Json;
        }



        public async Task<JioAuthResponse> GenerateSessionToken(string mobile)
        {
            try
            {
              
                string aesKey = _cache.GetOrCreate("JIO_AES_KEY", entry =>
                {
                    entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(7);
                    return JioAuthHelper.GenerateRandomString(16);
                });

    
                string secretKey = _config["JPBAEPS:SecretKey"];
                string encryptedValue = JioAuthHelper.EncryptAES(secretKey, aesKey);

                string pemPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "JioPaymentPEMFile", "prod_apigw_publicKey.pem");
                string publicKeyText = File.ReadAllText(pemPath);
                string rsaKey = JioAuthHelper.EncryptRSA(aesKey, publicKeyText);


                string url = _config["JPBAEPS:BaseUrl"] + _config["JPBAEPS:AuthSessionPath"];
                var client = _httpFactory.CreateClient("JIO");

                var deviceCfg = _config.GetSection("JPBAEPS:DeviceInfo");

                string ipAddress =
                    GetRemoteIpAddress() ??
                    deviceCfg.GetValue<string>("ipAddressFallback") ??
                    "0.0.0.0";

                var deviceInfo = new
                {
                    ipAddress = ipAddress,
                    type = deviceCfg.GetValue<string>("type") ?? "MOB",
                    os = deviceCfg.GetValue<string>("os") ?? "ANDROID",
                    appName = deviceCfg.GetValue<string>("appName") ?? "Jiopay",
                    appId = deviceCfg.GetValue<string>("appId") ?? "com.jiobank.aeps",
                    sdkVersion = deviceCfg.GetValue<string>("sdkVersion") ?? "1.0.0",
                    mobile = string.IsNullOrEmpty(mobile) ? null : mobile,
                    userAgent = deviceCfg.GetValue<string>("userAgent") ?? "JioBankSDK/1.0"
                };

                string deviceInfoJson = JsonConvert.SerializeObject(deviceInfo);

                var payload = new
                {
                    application = new
                    {
                        applicationName = _config["JPBAEPS:ApplicationName"],
                        clientId = _config["JPBAEPS:clientId"]
                    },
                    authenticateList = new[]
                    {
                        new { mode = 20, value = encryptedValue }
                    },
                    purpose = 2,
                    scope = _config["JPBAEPS:SessionScope"],
                    secure = new { encryptionKey = rsaKey }
                };

                var req = new HttpRequestMessage(HttpMethod.Post, url)
                {
                    Content = new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json")
                };

                req.Headers.Add("x-channel-id", _config["JPBAEPS:channelId"]);
                req.Headers.Add("x-trace-id", Guid.NewGuid().ToString());
                req.Headers.Add("x-device-info", deviceInfoJson);

                var response = await client.SendAsync(req);
                string respText = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                    return null;

                var json = JObject.Parse(respText);

                string accessToken = json["session"]?["accessToken"]?["tokenValue"]?.ToString();
                string refreshToken = json["session"]?["refreshToken"]?["tokenValue"]?.ToString();
                string appIdentifierToken = json["session"]?["appIdentifierToken"]?.ToString();

                if (string.IsNullOrEmpty(accessToken) ||
                    string.IsNullOrEmpty(refreshToken) ||
                    string.IsNullOrEmpty(appIdentifierToken))
                {
                    return null;
                }

                return new JioAuthResponse
                {
                    AccessToken = accessToken,
                    RefreshToken = refreshToken,
                    AppIdentifierToken = appIdentifierToken
                };
            }
            catch (Exception ex)
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


        //public async Task<BalanceInquiryResponseDto> BalanceInquiryAsync(BalanceInquiryRequestDto model, CancellationToken cancellationToken = default)
        //{
        //    if (model == null) throw new ArgumentNullException(nameof(model));
        //    if (string.IsNullOrWhiteSpace(model.AadhaarNumber))
        //        return new BalanceInquiryResponseDto { Success = false, Message = "Aadhaar is required", StatusCode = "33", accessToken = model.AccessToken, appIdentifierToken = model.AppIdentifierToken };
        //    if (string.IsNullOrWhiteSpace(model.PidXml))
        //        return new BalanceInquiryResponseDto { Success = false, Message = "PID XML required", StatusCode = "33", accessToken = model.AccessToken, appIdentifierToken = model.AppIdentifierToken };

        //    var cfg = _config.GetSection("JPBAEPS");
        //    string channelId = cfg.GetValue<string>("channelId") ?? "7215";
        //    string apiBase = (cfg.GetValue<string>("BaseUrl") ?? "").TrimEnd('/');
        //    string apiPath = cfg.GetValue<string>("BalanceInquiryPath") ?? ":9402/jpb/exp/v1/app/aeps/payouts/transaction";
        //    string apiUrl = apiBase.EndsWith(":9402") || apiBase.Contains(":9402") ? apiBase + apiPath.Replace(":9402", "") : apiBase + apiPath;

        //    string accessToken = model.AccessToken;
        //    string appIdToken = model.AppIdentifierToken;

        //    var deviceConfig = cfg.GetSection("DeviceInfo");
        //    string ipAddress = GetRemoteIpAddress() ?? deviceConfig.GetValue<string>("ipAddressFallback") ?? "0.0.0.0";
        //    var deviceInfoObj = new
        //    {
        //        ipAddress = ipAddress,
        //        type = deviceConfig.GetValue<string>("type") ?? "MOB",
        //        os = deviceConfig.GetValue<string>("os") ?? "ANDROID",
        //        appName = deviceConfig.GetValue<string>("appName") ?? "Jiopay",
        //        appId = deviceConfig.GetValue<string>("appId") ?? "JIOPAY001",
        //        sdkVersion = deviceConfig.GetValue<string>("sdkVersion") ?? "1.0.0",
        //        mobile = model.MobileNumber,
        //        userAgent = deviceConfig.GetValue<string>("userAgent") ?? "Jiopay-AgentApp"
        //    };
        //    string deviceInfoJson = System.Text.Json.JsonSerializer.Serialize(deviceInfoObj);

        //    string pidBase64 = "";
        //    try
        //    {
        //        pidBase64 = ConvertPidXmlToBase64(model.PidXml);
        //    }
        //    catch (Exception ex)
        //    {
        //        return new BalanceInquiryResponseDto { Success = false, Message = "PID conversion failed: " + ex.Message, StatusCode = "33", accessToken = model.AccessToken, appIdentifierToken = model.AppIdentifierToken };
        //    }

        //    DateTime istTime = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("India Standard Time"));
        //    string timestamp = istTime.ToString("yyyy-MM-dd'T'HH:mm:ss.000'Z'");

        //    string uid = DateTime.UtcNow.Ticks.ToString().Substring(0, 15);

        //    var body = new
        //    {
        //        transaction = new
        //        {
        //            idempotentKey = uid,
        //            currency = 356,
        //            invoice = uid,
        //            method = new { type = 312, subType = 550 },
        //            mode = 2,
        //            metadata = new
        //            {
        //                agent = new
        //                {
        //                    id = model.AgentLoginId,
        //                    subId = (string?)null,
        //                    address = new { stateCode = (string?)null, pinCode = model.AgentPin }
        //                }
        //            },
        //            captureMethod = 1,
        //            livemode = true,
        //            application = channelId,
        //            initiatingEntityTimestamp = timestamp,
        //            initiatingEntity = new { entityId = channelId, callbackUrl = (string?)null }
        //        },
        //        payer = new
        //        {
        //            mobile = new { number = model.MobileNumber, countryCode = "91" },
        //            type = 13,
        //            userId = (string?)null,
        //            bankId = model.BankId,
        //            bankName = model.BankName,
        //            aadhaar = new
        //            {
        //                aadhaarNumber = model.AadhaarNumber,
        //                consentCode = new
        //                {
        //                    id = "B88",
        //                    description = "I hereby provide my consent to Jio Payments Bank Limited (\"Bank\") to use my Aadhaar number and biometric authentication to verify my identity for the purpose of doing AePS transactions from my account (\"Service\"). JPB has informed me that my biometrics will not be stored/shared and will be submitted to CIDR only for the purpose of authentication. I have reviewed the transaction details and found to be correct. I understand and agree to the terms and conditions governing the Service as available on website www.jiobank.in and confirm that my biometric authentication be treated as my consent for availing the Service from the Bank. I hereby give my consent to receive promotional consent on behalf of the Bank.",
        //                    version = "1",
        //                    timeStamp = timestamp
        //                }
        //            }
        //        },
        //        secure = new
        //        {
        //            biometrics = new { fingerprint = pidBase64, type = 1 },
        //            deviceInfo = new
        //            {
        //                peripheral = channelId,
        //                source = new
        //                {
        //                    type = "MOB",
        //                    id = Guid.NewGuid().ToString("N").Substring(0, 16),
        //                    ip = ipAddress,
        //                    osType = deviceConfig.GetValue<string>("os") ?? "ANDROID",
        //                    osVer = "13",
        //                    model = "DeviceModel"
        //                },
        //                location = new { latitude = model.Latitude ?? "", longitude = model.Longitude ?? "" }
        //            }
        //        }
        //    };

        //    string requestBody = JsonConvert.SerializeObject(body);
        //    var client = _httpFactory.CreateClient("JIO");

        //    int attempts = 0;
        //    const int maxAttempts = 2;
        //    JioAuthResponse fallbackTokens = null;

        //    var userData = _context.TblUsers.Where(id => id.Id == Convert.ToInt32(model.UserId)).FirstOrDefault();

        //    var latestWallet = await _context.Tbluserbalances
        //       .Where(x => x.UserId == userData.Id)
        //       .OrderByDescending(x => x.Id)
        //       .FirstOrDefaultAsync();

        //    var currentBalance = latestWallet?.NewBal ?? 0;

        //    var tx = new TransactionDetail
        //    {
        //        UserId = Convert.ToString(userData.Id),
        //        UserName = userData?.Name + "-" + userData?.Phone,
        //        WlId = userData?.Wlid,
        //        MdId = userData?.Mdid,
        //        AdId = userData?.Adid,
        //        TxnId = uid,
        //        ServiceName = "AEPS",
        //        OperatorName = "AEPS_BALANCE_ENQUIRY",
        //        OpId = null,
        //        Mobileno = model.AadhaarNumber.Length >= 12 ? new string('X', 8) + model.AadhaarNumber.Substring(model.AadhaarNumber.Length - 4) : model.AadhaarNumber,
        //        OldBal = currentBalance,
        //        Amount = 0m,
        //        Comm = 0m,
        //        Charge = 0m,
        //        Cost = 0m,
        //        NewBal = Convert.ToString(currentBalance),
        //        Status = "INIT",
        //        Brid = null,
        //        TxnType = "Credit",
        //        ApiTxnId = uid,
        //        ApiName = "JPB BalanceInquiry",
        //        AdminRemarks = null,
        //        ApiMsg = null,
        //        ApiRes = null,
        //        ApiReq = Truncate(RedactSensitive(requestBody), 4000),
        //        ReqDate = DateTime.Now,
        //        UpdateDate = DateTime.Now,
        //        CustomerName = null,
        //        AccountNo = model.AadhaarNumber.Length >= 12 ? new string('X', 8) + model.AadhaarNumber.Substring(model.AadhaarNumber.Length - 4) : model.AadhaarNumber,
        //        ComingFrom = model.ComingFrom,
        //        IfscCode = null,
        //        BankName = model.BankName,
        //        Tds = 0m,
        //        TxnMode = null,
        //        MdComm = 0m,
        //        AdComm = 0m,
        //        WlComm = 0m,
        //        ServiceId = 5
        //    };

        //    // Save initial transaction to DB (so TransId exists)
        //    try
        //    {
        //        await _context.TransactionDetails.AddAsync(tx, cancellationToken);
        //        await _context.SaveChangesAsync(cancellationToken);
        //    }
        //    catch (Exception dbEx)
        //    {
        //        // if DB insert fails, still continue but log
        //        await LogApiAsync(apiUrl, "POST", "33", dbEx.Message, deviceInfoJson+",accesstoken: "+accessToken+", appidentifierToken: "+appIdToken+"", requestBody, null, "AEPS", "BalanceInquiry");
        //    }

        //    while (attempts < maxAttempts)
        //    {
        //        attempts++;
        //        using var req = new HttpRequestMessage(HttpMethod.Post, apiUrl);
        //        req.Content = new StringContent(requestBody, Encoding.UTF8, "application/json");

        //        // headers
        //        req.Headers.Add("x-channel-id", channelId);
        //        if (!string.IsNullOrEmpty(appIdToken)) req.Headers.Add("x-appid-token", appIdToken);
        //        if (!string.IsNullOrEmpty(accessToken)) req.Headers.Add("x-app-access-token", accessToken);
        //        req.Headers.Add("x-trace-id", Guid.NewGuid().ToString());
        //        req.Headers.Add("x-device-info", deviceInfoJson);

        //        var clientId = (cfg.GetValue<string>("clientId") ?? "");
        //        if (!string.IsNullOrEmpty(clientId)) req.Headers.Add("clientId", clientId);

        //        HttpResponseMessage resp = null;
        //        string responseContent = null;

        //        try
        //        {
        //            resp = await client.SendAsync(req, cancellationToken);
        //            responseContent = await resp.Content.ReadAsStringAsync(cancellationToken);


        //            // Log API call to JioPaymentAPILog (existing table)
        //            await LogApiAsync(apiUrl, "POST", resp.IsSuccessStatusCode ? "00" : resp.StatusCode.ToString(),
        //                              null, deviceInfoJson + ",accesstoken: " + accessToken + ", appidentifierToken: " + appIdToken + "", requestBody, responseContent, "AEPS", "BalanceInquiry");


        //            tx.ApiRes = responseContent;
        //            _context.TransactionDetails.Update(tx);
        //            _context.SaveChanges();

        //            // update DB row with API response and UpdateDate
        //            try
        //            {
        //                tx.ApiRes = Truncate(RedactSensitive(responseContent), 4000);
        //                tx.UpdateDate = DateTime.Now;
        //                if (resp.IsSuccessStatusCode)
        //                {
        //                    tx.Status = "SUCCESS";
        //                }
        //                else
        //                {
        //                    tx.Status = "FAILED";
        //                }

        //                _context.TransactionDetails.Update(tx);
        //                await _context.SaveChangesAsync(cancellationToken);
        //            }
        //            catch
        //            {
        //                // ignore DB update errors but continue
        //            }

        //            var parsed = JObject.Parse(responseContent);
        //            string errorCode = parsed["error"]?["code"]?.ToString();
        //            string errormsg = parsed["error"]?["message"]?.ToString();
        //            string responseCode = parsed["responseCode"]?.ToString();
        //            string responseMessage = parsed["responseMessage"]?.ToString();
        //            var rData = parsed["responseData"];
        //            string apiTxnId = rData?["transaction"]?["transactionId"]?.ToString();
        //            string rrn = rData?["transaction"]?["rrn"]?.ToString();
        //            string txnId = rData?["transaction"]?["transactionId"]?.ToString();
        //            decimal? balance = null;
        //            if (rData?["account"]?["balance"] != null && decimal.TryParse(rData["account"]["balance"].ToString(), out var bval))
        //                balance = bval;
        //            string accExists = rData?["account"]?["accountExist"]?.ToString();

        //            if (resp.IsSuccessStatusCode && errorCode != "2369" && responseCode == "00")
        //            {
        //                // parse expected fields (responseContent JSON)
        //                try
        //                {


        //                    // update transaction row with parsed info
        //                    try
        //                    {
        //                        tx.ApiTxnId = apiTxnId ?? tx.ApiTxnId;
        //                        tx.ApiMsg = string.IsNullOrEmpty(responseMessage) ? errormsg : Truncate(responseMessage, 500);
        //                        tx.UpdateDate = DateTime.Now;
        //                        tx.Status = (responseCode == "00" && string.Equals(responseCode, "SUCCESS", StringComparison.OrdinalIgnoreCase)) ? "SUCCESS" : tx.Status;
        //                        //tx.NewBal = balance?.ToString("F2");
        //                        _context.TransactionDetails.Update(tx);
        //                        await _context.SaveChangesAsync(cancellationToken);
        //                    }
        //                    catch { /* ignore DB update errors */ }

        //                    return new BalanceInquiryResponseDto
        //                    {
        //                        Success = true,
        //                        Message = responseMessage ?? errormsg,
        //                        StatusCode = responseCode ?? errorCode,
        //                        RawResponse = responseContent,
        //                        ApiTxnId = apiTxnId,
        //                        Rrn = rrn,
        //                        TransactionId = txnId,
        //                        Balance = balance,
        //                        AccountExists = accExists,
        //                        TransId = tx.TransId,
        //                        accessToken = accessToken,
        //                        appIdentifierToken = appIdToken
        //                    };
        //                }
        //                catch (Exception parseEx)
        //                {
        //                    return new BalanceInquiryResponseDto
        //                    {
        //                        Success = true,
        //                        Message = "Success but parse failed: " + parseEx.Message,
        //                        StatusCode = "00",
        //                        RawResponse = responseContent,
        //                        TransId = tx.TransId,
        //                        accessToken = accessToken,
        //                        appIdentifierToken = appIdToken
        //                    };
        //                }
        //            }

        //            // Not success: detect session-expiry like CreateAgent
        //            bool isSessionExpired = false;
        //            try
        //            {
        //                using var doc = JsonDocument.Parse(responseContent);
        //                if (doc.RootElement.TryGetProperty("error", out var errEls) && errEls.TryGetProperty("code", out var codeEl))
        //                {
        //                    var code = codeEl.GetString();

        //                    if (code == "2369" || code == "33306")
        //                    {
        //                        isSessionExpired = true;
        //                    }
        //                }

        //                if (!isSessionExpired && doc.RootElement.TryGetProperty("message", out var msgEl))
        //                {
        //                    var msg = msgEl.GetString();
        //                    if (!string.IsNullOrEmpty(msg) && msg.IndexOf("Invalid Session", StringComparison.OrdinalIgnoreCase) >= 0)
        //                        isSessionExpired = true;
        //                }


        //                if (doc.RootElement.TryGetProperty("error", out var errEl) && errEl.TryGetProperty("code", out var errCodeEl))
        //                {
        //                    var ecode = errCodeEl.GetString();
        //                    if (ecode == "2369" || ecode == "33306") isSessionExpired = true;
        //                }

        //            }
        //            catch
        //            {
        //                if ((!string.IsNullOrEmpty(responseContent) && responseContent.Contains("33306")) || (!string.IsNullOrEmpty(responseContent) && responseContent.Contains("2369")))
        //                    isSessionExpired = true;
        //                if (!string.IsNullOrEmpty(responseContent) && responseContent.IndexOf("Invalid Session", StringComparison.OrdinalIgnoreCase) >= 0)
        //                    isSessionExpired = true;
        //            }

        //            if (isSessionExpired && attempts < maxAttempts)
        //            {
        //                fallbackTokens = await GenerateSessionToken(model.MobileNumber);
        //                if (fallbackTokens == null)
        //                {
        //                    tx.Status = "FAILED";
        //                    tx.ApiRes = Truncate(RedactSensitive(responseContent), 4000);
        //                    tx.UpdateDate = DateTime.Now;
        //                    await _context.SaveChangesAsync(cancellationToken);
        //                    return new BalanceInquiryResponseDto { Success = false, Message = "Session expired and refresh failed", StatusCode = "22", RawResponse = responseContent, TransId = tx.TransId,
        //                        accessToken = accessToken,
        //                        appIdentifierToken = appIdToken
        //                    };
        //                }

        //                accessToken = fallbackTokens.AccessToken;
        //                appIdToken = fallbackTokens.AppIdentifierToken;
        //                await LogApiAsync(apiUrl, "POST", "33", "New Access Token After Session Destroy", deviceInfoJson + ",accesstoken: " + accessToken + ", appidentifierToken: " + appIdToken + "", requestBody, null, "AEPS", "BalanceInquiry");
        //                continue; // retry
        //            }

        //            // final failure
        //            return new BalanceInquiryResponseDto
        //            {
        //                Success = false,
        //                Message = responseMessage ?? errormsg,
        //                StatusCode = responseCode ?? errorCode,
        //                RawResponse = responseContent,
        //                TransId = tx.TransId,
        //                accessToken = accessToken,
        //                appIdentifierToken = appIdToken
        //            };
        //        }
        //        catch (OperationCanceledException oce) when (cancellationToken.IsCancellationRequested)
        //        {
        //            tx.Status = "CANCELLED";
        //            tx.UpdateDate = DateTime.Now;
        //            await _context.SaveChangesAsync(cancellationToken);
        //            return new BalanceInquiryResponseDto { Success = false, Message = "Request canceled", StatusCode = "33", TransId = tx.TransId,
        //                accessToken = accessToken,
        //                appIdentifierToken = appIdToken
        //            };
        //        }
        //        catch (Exception ex)
        //        {
        //            // Log and update DB
        //            await LogApiAsync(apiUrl, "POST", "33", ex.Message, deviceInfoJson + ",accesstoken: " + accessToken + ", appidentifierToken: " + appIdToken + "", requestBody, null, "AEPS", "BalanceInquiry");
        //            try
        //            {
        //                tx.Status = "FAILED";
        //                tx.AdminRemarks = ex.Message;
        //                tx.UpdateDate = DateTime.Now;
        //                _context.TransactionDetails.Update(tx);
        //                await _context.SaveChangesAsync(cancellationToken);
        //            }
        //            catch { }
        //            return new BalanceInquiryResponseDto { Success = false, Message = "Unexpected error: " + ex.Message, StatusCode = "33", TransId = tx.TransId, accessToken = accessToken,
        //                appIdentifierToken = appIdToken
        //            };
        //        }
        //    } // end while

        //    return new BalanceInquiryResponseDto { Success = false, Message = "Unhandled flow", StatusCode = "33",
        //        accessToken = accessToken,
        //        appIdentifierToken = appIdToken
        //    };
        //}


        public async Task<BalanceInquiryResponseDto> BalanceInquiryAsync(BalanceInquiryRequestDto model, CancellationToken cancellationToken = default)
        {
            if (model == null) throw new ArgumentNullException(nameof(model));
            if (string.IsNullOrWhiteSpace(model.AadhaarNumber))
                return Error("Aadhaar is required", model, "33");


            if (string.IsNullOrWhiteSpace(model.PidXml))
                return Error("PID XML required", model, "33");

            var cfg = _config.GetSection("JPBAEPS");
            string channelId = cfg.GetValue<string>("channelId") ?? "7215";
            string apiBase = (cfg.GetValue<string>("BaseUrl") ?? "").TrimEnd('/');
            string apiPath = cfg.GetValue<string>("BalanceInquiryPath") ?? ":9402/jpb/exp/v1/app/aeps/payouts/transaction";
            string apiUrl = BuildUrl(apiBase, apiPath);


            string accessToken = model.AccessToken;
            string appIdToken = model.AppIdentifierToken;

            string deviceInfoJson = JsonConvert.SerializeObject(BuildDeviceInfo(cfg, model));

            string pidBase64;
            try { pidBase64 = ConvertPidXmlToBase64(model.PidXml); }
            catch (Exception ex) {
                return new BalanceInquiryResponseDto { Success = false, Message = "finger print data is missing", StatusCode = "33", accessToken= accessToken, appIdentifierToken= appIdToken };
               
            }

            string timestamp = GetIndianTimestamp();
            string uid = GenerateUid();

            string requestBody = JsonConvert.SerializeObject(BuildRequestBody(model, pidBase64, channelId, timestamp, uid, deviceInfoJson));
            var client = _httpFactory.CreateClient("JIO");

            var tx = await CreateTransactionRow(model, uid, requestBody);

            const int maxAttempts = 2;

            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                uid = GenerateUid();
                timestamp = GetIndianTimestamp();
                string traceId = Guid.NewGuid().ToString();
                requestBody = JsonConvert.SerializeObject(BuildRequestBody(model, pidBase64, channelId, timestamp, uid, deviceInfoJson));
                var response = await SendJioRequest(apiUrl, requestBody, deviceInfoJson, accessToken, appIdToken, channelId, client, cancellationToken, traceId);
                await LogApiAsync(apiUrl, "POST", response.StatusCode, response.ErrorMessage,
                                    deviceInfoJson + $", attempt={attempt}, accessToken={accessToken}, appToken={appIdToken}, x-traceid={traceId}",
                                    requestBody, response.Raw, "AEPS", "BalanceInquiry");
                bool sessionExpired = IsSessionExpired(response.Raw);

                if (sessionExpired)
                {
                    await UpdateTx(tx, "FAILED", "Session expired", response.Raw);
                    if (attempt == maxAttempts)
                    {
                        return ErrorWithTx("Session expired after retries", tx, "2369", response.Raw, accessToken, appIdToken);
                    }

                    var tokens = await GenerateSessionToken(model.MobileNumber);
                    if (tokens == null)
                    {
                        return ErrorWithTx("Session expired & token regeneration failed", tx, "2369", response.Raw, accessToken, appIdToken);
                    }

                    accessToken = tokens.AccessToken;
                    appIdToken = tokens.AppIdentifierToken;
                    continue;

                }

                var parsed = TryParseResponse(response.Raw);
                await UpdateTx(tx, parsed.IsSuccess ? "SUCCESS" : "FAILED", parsed.Message, response.Raw, parsed.ApiTxnId, parsed.Rrn);

                return new BalanceInquiryResponseDto
                {
                    Success = parsed.IsSuccess,
                    Message = parsed.Message,
                    StatusCode = parsed.Code,
                    RawResponse = response.Raw,
                    ApiTxnId = parsed.ApiTxnId,
                    TransactionId = parsed.TransactionId,
                    Rrn = parsed.Rrn,
                    Balance = parsed.Balance,
                    AccountExists = parsed.AccountExists,
                    TransId = tx.TransId,
                    accessToken = accessToken,
                    appIdentifierToken = appIdToken
                };

            }
            return Error("Unhandled flow", model, "33");
        }

        private BalanceInquiryResponseDto Error(string msg, BalanceInquiryRequestDto model, string code)
        {
            return new BalanceInquiryResponseDto { Success = false, Message = msg, StatusCode = code, accessToken = model.AccessToken, appIdentifierToken = model.AppIdentifierToken };
        }

        private BalanceInquiryResponseDto ErrorWithTx(string msg, TransactionDetail tx, string code, string raw, string access, string app)
        {
            return new BalanceInquiryResponseDto
            {
                Success = false,
                Message = msg,
                StatusCode = code,
                RawResponse = raw,
                TransId = tx.TransId,
                accessToken = access,
                appIdentifierToken = app
            };
        }

        private string BuildUrl(string apiBase, string apiPath)
        {
            return apiBase.EndsWith(":9402") ? apiBase + apiPath.Replace(":9402", "") : apiBase + apiPath;
        }

        private object BuildDeviceInfo(IConfigurationSection cfg, BalanceInquiryRequestDto model)
        {
            var deviceCfg = cfg.GetSection("DeviceInfo");
            string ip = GetRemoteIpAddress() ?? deviceCfg.GetValue<string>("ipAddressFallback") ?? "0.0.0.0";


            return new
            {
                ipAddress = ip,
                type = deviceCfg.GetValue<string>("type") ?? "MOB",
                os = deviceCfg.GetValue<string>("os") ?? "ANDROID",
                appName = deviceCfg.GetValue<string>("appName") ?? "Jiopay",
                appId = deviceCfg.GetValue<string>("appId") ?? "JIOPAY001",
                sdkVersion = deviceCfg.GetValue<string>("sdkVersion") ?? "1.0.0",
                mobile = model.MobileNumber,
                userAgent = deviceCfg.GetValue<string>("userAgent") ?? "Jiopay-AgentApp"
            };
        }

        private string GetIndianTimestamp()
        {
            var ist = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("India Standard Time"));
            return ist.ToString("yyyy-MM-dd'T'HH:mm:ss.000'Z'");
        }

        private string GenerateUid() => DateTime.UtcNow.Ticks.ToString().Substring(0, 15);

        private object BuildRequestBody(BalanceInquiryRequestDto m, string pidBase64, string channelId, string timestamp, string uid, string deviceInfo)
        {
            return new
            {
                transaction = new
                {
                    idempotentKey = uid,
                    currency = 356,
                    invoice = uid,
                    method = new { type = 312, subType = 550 },
                    mode = 2,
                    metadata = new
                    {
                        agent = new
                        {
                            id = m.AgentLoginId,
                            subId = (string)null,
                            address = new { stateCode = (string)null, pinCode = m.AgentPin }
                        }
                    },
                    captureMethod = 1,
                    livemode = true,
                    application = channelId,
                    initiatingEntityTimestamp = timestamp,
                    initiatingEntity = new { entityId = channelId, callbackUrl = (string)null }
                },
                payer = new
                {
                    mobile = new { number = m.MobileNumber, countryCode = "91" },
                    type = 13,
                    userId = (string)null,
                    bankId = m.BankId,
                    bankName = m.BankName,
                    aadhaar = new
                    {
                        aadhaarNumber = m.AadhaarNumber,
                        consentCode = new
                        {
                            id = "B88",
                            description = "I hereby provide my consent...",
                            version = "1",
                            timeStamp = timestamp
                        }
                    }
                },
                secure = new
                {
                    biometrics = new { fingerprint = pidBase64, type = 1 },
                    deviceInfo = new
                    {
                        peripheral = channelId,
                        source = new
                        {
                            type = "MOB",
                            id = Guid.NewGuid().ToString("N").Substring(0, 16),
                            ip = ((dynamic)JsonConvert.DeserializeObject(deviceInfo)).ipAddress,
                            osType = "ANDROID",
                            osVer = "13",
                            model = "DeviceModel"
                        },
                        location = new { latitude = m.Latitude ?? "", longitude = m.Longitude ?? "" }
                    }
                }
            };
        }

        private async Task<TransactionDetail> CreateTransactionRow(BalanceInquiryRequestDto model, string uid, string request)
        {
            var user = _context.TblUsers.First(x => x.Id == Convert.ToInt32(model.UserId));
            var wallet = await _context.Tbluserbalances.Where(x => x.UserId == user.Id).OrderByDescending(x => x.Id).FirstOrDefaultAsync();


            var tx = new TransactionDetail
            {
                UserId = user.Id.ToString(),
                UserName = user.Name + "-" + user.Phone,
                WlId = user.Wlid,
                MdId = user.Mdid,
                AdId = user.Adid,
                TxnId = uid,
                ServiceName = "AEPS",
                OperatorName = "AEPS_BALANCE_ENQUIRY",
                Mobileno = Mask(model.AadhaarNumber),
                OldBal = wallet?.NewBal ?? 0,
                Amount = 0,
                NewBal = wallet?.NewBal.ToString(),
                Status = "INIT",
                ApiReq = Truncate(RedactSensitive(request), 4000),
                ReqDate = DateTime.Now,
                UpdateDate = DateTime.Now,
                BankName = model.BankName,
                AccountNo = Mask(model.AadhaarNumber),
                ComingFrom = model.ComingFrom,
                ServiceId = 5
            };


            _context.TransactionDetails.Add(tx);
            await _context.SaveChangesAsync();
            return tx;
        }
        private string Mask(string aadhaar)
        {
            return aadhaar.Length >= 12 ? new string('X', 8) + aadhaar[^4..] : aadhaar;
        }

        private async Task UpdateTx(TransactionDetail tx, string status, string msg, string raw, string apiTxnId = null, string RRN = null)
        {
            tx.Status = status;
            tx.ApiMsg = msg;
            tx.ApiRes = Truncate(RedactSensitive(raw), 4000);
            if (apiTxnId != null) tx.ApiTxnId = apiTxnId;
            tx.UpdateDate = DateTime.Now;
            tx.Brid = RRN;
            _context.TransactionDetails.Update(tx);
            await _context.SaveChangesAsync();
        }

        private async Task<(string Raw, string StatusCode, string ErrorMessage)> SendJioRequest(
string url, string body, string deviceInfo, string access, string appToken, string channel,
HttpClient client, CancellationToken ct, string TraceId)
        {
            try
            {
                using var req = new HttpRequestMessage(HttpMethod.Post, url);
                req.Content = new StringContent(body, Encoding.UTF8, "application/json");
                req.Headers.Add("x-channel-id", channel);
                req.Headers.Add("x-device-info", deviceInfo);
                req.Headers.Add("x-trace-id", TraceId);
                if (!string.IsNullOrEmpty(appToken)) req.Headers.Add("x-appid-token", appToken);
                if (!string.IsNullOrEmpty(access)) req.Headers.Add("x-app-access-token", access);


                var resp = await client.SendAsync(req, ct);
                var content = await resp.Content.ReadAsStringAsync();
                return (content, resp.StatusCode.ToString(), resp.IsSuccessStatusCode ? null : resp.ReasonPhrase);
            }
            catch (Exception ex)
            {
                return ($"{{ \"error\": \"{ex.Message}\" }}", "ERROR", ex.Message);
            }
        }

        private bool IsSessionExpired(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return false;


            try
            {
                var j = JObject.Parse(raw);


                string code = j["error"]?["code"]?.ToString();
                string msg = j["error"]?["message"]?.ToString();


                if (code == "2369" || code == "33306") return true;
                if (!string.IsNullOrEmpty(msg) && msg.Contains("Invalid Session", StringComparison.OrdinalIgnoreCase)) return true;
            }
            catch
            {
                if (raw.Contains("2369") || raw.Contains("33306") || raw.Contains("Invalid Session"))
                    return true;
            }


            return false;
        }

        private (bool IsSuccess, string Message, string Code, string ApiTxnId, string TransactionId, string Rrn, decimal? Balance, string AccountExists) TryParseResponse(string raw)
        {
            try
            {
                var j = JObject.Parse(raw);
                string code = j["responseCode"]?.ToString() ?? j["error"]?["code"]?.ToString();
                string msg = j["responseMessage"]?.ToString() ?? j["error"]?["message"]?.ToString();
                var data = j["responseData"];
                string apiTxnId = data?["transaction"]?["transactionId"]?.ToString();
                string rrn = data?["transaction"]?["rrn"]?.ToString();
                string txnId = data?["transaction"]?["transactionId"]?.ToString();
                decimal? balance = null;
                if (decimal.TryParse(data?["account"]?["balance"]?.ToString(), out var b)) balance = b;
                string accExists = data?["account"]?["accountExist"]?.ToString();
                return (code == "00", msg, code, apiTxnId, txnId, rrn, balance, accExists);
            }
            catch
            {
                return (false, "Parse failed", "33", null, null, null, null, null);
            }
        }


    }

    public class JioAuthResponse
    {
        public string AccessToken { get; set; }
        public string RefreshToken { get; set; }
        public string AppIdentifierToken { get; set; }
    }

}





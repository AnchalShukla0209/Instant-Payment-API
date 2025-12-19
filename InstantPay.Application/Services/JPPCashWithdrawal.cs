using InstantPay.Application.Interfaces;
using InstantPay.Infrastructure.Sql.Entities;
using InstantPay.SharedKernel.RequestPayload;
using InstantPay.SharedKernel.Results;
using InstantPay.SharedKernel.Results.CashWithdrawal;
using InstantPay.SharedKernel.Results.MiniStatement;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Transactions;
using System.Xml.Linq;
using static MongoDB.Driver.WriteConcern;
using static Org.BouncyCastle.Math.EC.ECCurve;

namespace InstantPay.Application.Services
{
    public class JPPCashWithdrawal : IJPPCashWithdrawal
    {
        private readonly AppDbContext _context;
        private readonly IHttpClientFactory _httpFactory;
        private readonly IConfiguration _config;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly TimeSpan _cacheDuration;
        public JPPCashWithdrawal(AppDbContext context, IHttpClientFactory httpFactory, IConfiguration config, IHttpContextAccessor httpContextAccessor)
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
        public async Task<CashWithdrawalResponseDto> CashWithdrawalAsync(CashWithdrawalRequest model, CancellationToken cancellationToken = default)
        {
            if (model == null) throw new ArgumentNullException(nameof(model));
            if (string.IsNullOrWhiteSpace(model.Aadhaar)) return new CashWithdrawalResponseDto { Success = false, ResponseMessage = "Aadhaar is required", ResponseCode = "33", accessToken = model.AccessToken, appIdentifierToken = model.AppIdentifierToken };
            if (string.IsNullOrWhiteSpace(model.FingerprintXml)) return new CashWithdrawalResponseDto { Success = false, ResponseMessage = "PID XML or PidBase64 required", ResponseCode = "33", accessToken = model.AccessToken, appIdentifierToken = model.AppIdentifierToken };
            if (model.Amount <= 0) return new CashWithdrawalResponseDto { Success = false, ResponseMessage = "Amount is required", ResponseCode = "33", accessToken = model.AccessToken, appIdentifierToken = model.AppIdentifierToken };

            var cfg = _config.GetSection("JPBAEPS");
            string channelId = cfg.GetValue<string>("channelId") ?? "7215";
            string apiBase = (cfg.GetValue<string>("BaseUrl") ?? "").TrimEnd('/');
            string apiPath = cfg.GetValue<string>("CashWithdrawalPath") ?? ":9402/jpb/exp/v1/app/aeps/payouts/transaction";
            string apiUrl = apiBase.EndsWith(":9402") || apiBase.Contains(":9402") ? apiBase + apiPath.Replace(":9402", "") : apiBase + apiPath;

            string accessToken = model.AccessToken;
            string appIdToken = model.AppIdentifierToken;

            var deviceConfig = cfg.GetSection("DeviceInfo");
            string ipAddress = GetRemoteIpAddress() ?? deviceConfig.GetValue<string>("ipAddressFallback") ?? "0.0.0.0";
            var deviceInfoObj = new
            {
                ipAddress = ipAddress,
                type = deviceConfig.GetValue<string>("type") ?? "MOB",
                os = deviceConfig.GetValue<string>("os") ?? "ANDROID",
                appName = deviceConfig.GetValue<string>("appName") ?? "JIO Payments Bank",
                appId = deviceConfig.GetValue<string>("appId") ?? "JIOPAY001",
                sdkVersion = deviceConfig.GetValue<string>("sdkVersion") ?? "1.0.0",
                mobile = model.Mobile,
                userAgent = deviceConfig.GetValue<string>("userAgent") ?? "Jiopay-AgentApp"
            };
            string deviceInfoJson = System.Text.Json.JsonSerializer.Serialize(deviceInfoObj);

            // PID -> base64
            string pidBase64 = "";
            try
            {
                pidBase64 = ConvertPidXmlToBase64(model.FingerprintXml);
            }
            catch (Exception ex)
            {
                return new CashWithdrawalResponseDto { Success = false, ResponseMessage = "finger print data is missing", ResponseCode = "33", accessToken = accessToken, appIdentifierToken = appIdToken };
            }

            // timestamp (IST) and uid
            DateTime istTime = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("India Standard Time"));
            string timestamp = istTime.ToString("yyyy-MM-dd'T'HH:mm:ss.000'Z'");
            string uid = DateTime.UtcNow.Ticks.ToString().Substring(0, 15);

            // Build request JSON (anonymous object)
            var body = new
            {
                transaction = new
                {
                    idempotentKey = uid,
                    currency = 356,
                    invoice = uid,
                    method = new { type = 115, subType = 550 },
                    mode = 2,
                    captureMethod = 1,
                    livemode = "true",
                    application = channelId,
                    initiatingEntityTimestamp = timestamp,
                    initiatingEntity = new { entityId = channelId },
                    metadata = new
                    {
                        agent = new
                        {
                            id = model.AgentLoginId,
                            address = new { pinCode = model.AgentPin }
                        }
                    }
                },
                amount = new
                {
                    netAmount = model.Amount.ToString(CultureInfo.InvariantCulture),
                    grossAmount = model.Amount.ToString(CultureInfo.InvariantCulture)
                },
                payer = new
                {
                    type = 13,
                    mobile = new { number = model.Mobile, countryCode = "91" },
                    bankId = model.BankId,
                    bankName = model.BankName,
                    aadhaar = new
                    {
                        aadhaarNumber = model.Aadhaar,
                        consentCode = new
                        {
                            id = "B88",
                            description = "I hereby provide my consent to Jio Payments Bank Limited (\"Bank\") to use my Aadhaar number and biometric authentication to verify my identity for AEPS transactions.",
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
                            ip = ipAddress,
                            osType = deviceConfig.GetValue<string>("os") ?? "ANDROID",
                            osVer = "13",
                            model = "DeviceModel"
                        },
                        location = new { latitude = model.Latitude ?? "", longitude = model.Longitude ?? "" }
                    }
                }
            };

            string requestBody = JsonConvert.SerializeObject(body);
            var client = _httpFactory.CreateClient("JIO");

            int attempts = 0;
            const int maxAttempts = 2;
            JioAuthResponse fallbackTokens = null;

            var userData = _context.TblUsers.Where(id => id.Id == Convert.ToInt32(model.UserId)).FirstOrDefault();

            var latestWallet = await _context.Tbluserbalances
                .Where(x => x.UserId == userData.Id)
                .OrderByDescending(x => x.Id)
                .FirstOrDefaultAsync(cancellationToken);

            var currentBalance = latestWallet?.NewBal ?? 0m;

            var tx = new TransactionDetail
            {
                UserId = Convert.ToString(userData.Id),
                UserName = userData?.Name + "-" + userData?.Phone,
                WlId = userData?.Wlid,
                MdId = userData?.Mdid,
                AdId = userData?.Adid,
                TxnId = uid,
                ServiceName = "AEPS",
                OperatorName = "AEPS_CASH_WITHDRAWAL",
                OpId = null,
                Mobileno = model.Aadhaar.Length >= 12 ? new string('X', 8) + model.Aadhaar.Substring(model.Aadhaar.Length - 4) : model.Aadhaar,
                OldBal = currentBalance,
                Amount = model.Amount,
                Comm = 0m,
                Charge = 0m,
                Cost = 0m,
                NewBal = Convert.ToString(currentBalance),
                Status = "INIT",
                Brid = null,
                TxnType = "Credit",
                ApiTxnId = uid,
                ApiName = "JPB CashWithdrawal",
                AdminRemarks = null,
                ApiMsg = null,
                ApiRes = null,
                ApiReq = Truncate(RedactSensitive(requestBody), 4000),
                ReqDate = DateTime.Now,
                UpdateDate = DateTime.Now,
                CustomerName = null,
                AccountNo = model.Aadhaar.Length >= 12 ? new string('X', 8) + model.Aadhaar.Substring(model.Aadhaar.Length - 4) : model.Aadhaar,
                ComingFrom = model.ComingFrom,
                IfscCode = null,
                BankName = model.BankName,
                Tds = 0m,
                TxnMode = null,
                MdComm = 0m,
                AdComm = 0m,
                WlComm = 0m,
                ServiceId = 5
            };

            try
            {
                await _context.TransactionDetails.AddAsync(tx, cancellationToken);
                await _context.SaveChangesAsync(cancellationToken);
            }
            catch (Exception dbEx)
            {
                await LogApiAsync(apiUrl, "POST", "33", dbEx.Message, deviceInfoJson, requestBody, null, "AEPS", "CashWithdrawal");
            }

            while (attempts < maxAttempts)
            {
                string traceId = Guid.NewGuid().ToString();
                attempts++;
                using var req = new HttpRequestMessage(HttpMethod.Post, apiUrl);
                req.Content = new StringContent(requestBody, Encoding.UTF8, "application/json");

                // headers
                req.Headers.Add("x-channel-id", channelId);

                if (!string.IsNullOrEmpty(appIdToken)) req.Headers.Add("x-appid-token", appIdToken);
                if (!string.IsNullOrEmpty(accessToken)) req.Headers.Add("x-app-access-token", accessToken);
                req.Headers.Add("x-trace-id", traceId);
                req.Headers.Add("x-device-info", deviceInfoJson);

                var clientId = (cfg.GetValue<string>("clientId") ?? "");
                if (!string.IsNullOrEmpty(clientId)) req.Headers.Add("clientId", clientId);

                HttpResponseMessage resp = null;
                string responseContent = null;

                try
                {
                    resp = await client.SendAsync(req, cancellationToken);
                    responseContent = await resp.Content.ReadAsStringAsync(cancellationToken);

                    await LogApiAsync(apiUrl, "POST", resp.IsSuccessStatusCode ? "00" : resp.StatusCode.ToString(),
                                      null, deviceInfoJson + "x-appid-token:" + appIdToken + ",x-app-access-token:" + accessToken+", traceid: "+ traceId + "", requestBody, responseContent, "AEPS", "CashWithdrawal");

                    tx.ApiRes = responseContent;
                    _context.TransactionDetails.Update(tx);
                    _context.SaveChanges();

                    // Update transaction row with ApiRes and Status
                    try
                    {

                        tx.ApiRes = Truncate(RedactSensitive(responseContent), 4000);
                        tx.UpdateDate = DateTime.Now;

                        _context.TransactionDetails.Update(tx);
                        await _context.SaveChangesAsync(cancellationToken);
                    }
                    catch { /* ignore DB update errors */ }
                    bool isSessionExpired = false;

                    using var docD = JsonDocument.Parse(responseContent);
                    if (docD.RootElement.TryGetProperty("code", out var codeEls))
                    {
                        var codeString = codeEls.GetRawText().Trim('"');
                        if (codeString == "33306") isSessionExpired = true;
                    }
                    if (!isSessionExpired && docD.RootElement.TryGetProperty("message", out var msgEls))
                    {
                        var msg = msgEls.GetString();
                        if (!string.IsNullOrEmpty(msg) && msg.IndexOf("Invalid Session", StringComparison.OrdinalIgnoreCase) >= 0)
                            isSessionExpired = true;
                    }
                    if (!isSessionExpired)
                    {
                        if (docD.RootElement.TryGetProperty("error", out var errEl) && errEl.TryGetProperty("code", out var errCodeEl))
                        {
                            var ecode = errCodeEl.GetString();
                            if (ecode == "33306" || ecode == "2369") isSessionExpired = true;
                        }
                    }

                    if (!isSessionExpired)
                    {
                        if (resp.IsSuccessStatusCode)
                        {
                            // parse into strongly typed model (your API DTO)
                            try
                            {
                                var parsed = JsonConvert.DeserializeObject<CashWithdrawalResponseDto>(responseContent);

                                try
                                {
                                    if (parsed?.ResponseData?.Transaction != null)
                                    {
                                        int slabId = model.Amount switch
                                        {
                                            0 => 0,
                                            <= 500 => 47,
                                            <= 2999 => 48,
                                            3000 => 49,
                                            <= 10000 => 50,
                                            _ => 0
                                        };

                                        int mainPlanId = Convert.ToInt32(userData.PlanId);
                                        decimal rtComm = await GetCommissionAsync(mainPlanId, slabId, model.Amount, "RT");
                                        decimal adComm = await GetCommissionAsync(mainPlanId, slabId, model.Amount, "AD");
                                        decimal mdComm = await GetCommissionAsync(mainPlanId, slabId, model.Amount, "MD");
                                        decimal wlComm = await GetCommissionAsync(mainPlanId, slabId, model.Amount, "WL");

                                        if (userData.Usertype == "RT")
                                        {
                                            if (userData.Adid == "0" && userData.Mdid == "0")
                                            {
                                                wlComm -= rtComm;
                                                adComm = 0;
                                                mdComm = 0;
                                            }
                                            else if (userData.Adid == "0" && userData.Mdid != "0")
                                            {
                                                mdComm -= rtComm;
                                                wlComm -= mdComm;
                                                adComm = 0;
                                            }
                                            else if (userData.Adid != "0" && userData.Mdid != "0")
                                            {
                                                adComm -= rtComm;
                                                mdComm -= adComm;
                                                wlComm -= mdComm;
                                            }
                                            else if (userData.Adid != "0" && userData.Mdid == "0")
                                            {
                                                adComm -= rtComm;
                                                mdComm = 0;
                                                wlComm -= adComm;
                                            }
                                        }

                                        tx.Comm = rtComm;
                                        tx.AdComm = adComm;
                                        tx.MdComm = mdComm;
                                        tx.WlComm = wlComm;
                                        tx.Tds = rtComm * 5 / 100;

                                        decimal tds = rtComm * 5 / 100;
                                        decimal cost = (model.Amount + rtComm) - tds;
                                        decimal Newbal = currentBalance + cost;

                                        tx.ApiTxnId = parsed.ResponseData.Transaction.TransactionId ?? tx.ApiTxnId;
                                        tx.ApiMsg = Truncate(parsed.ResponseMessage, 500);

                                        // compute new balance using Option B (Old + Amount)
                                        decimal computedNewBal = currentBalance + model.Amount;

                                        // prefer API returned balance if parseable
                                        if (parsed.ResponseData?.Account?.Balance != null)
                                        {
                                            if (decimal.TryParse(parsed.ResponseData.Account.Balance, NumberStyles.Number | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var parsedBal))
                                            {
                                                computedNewBal = parsedBal;
                                            }
                                        }
                                        tx.NewBal = parsed.ResponseCode == "00" && parsed.ResponseMessage.ToUpper() == "SUCCESS" ? Convert.ToString(Newbal) : Convert.ToString(currentBalance);
                                        tx.UpdateDate = DateTime.Now;
                                        tx.Status = parsed.ResponseCode == "00" && parsed.ResponseMessage.ToUpper() == "SUCCESS" ? "SUCCESS" : "FAILED";
                                        tx.Brid = parsed.ResponseData.Transaction.Rrn ?? tx.Brid;
                                        _context.TransactionDetails.Update(tx);
                                        await _context.SaveChangesAsync(cancellationToken);

                                        // insert single Tbluserbalance entry
                                        try
                                        {
                                            if (parsed.ResponseCode == "00" && parsed.ResponseMessage.ToUpper() == "SUCCESS")
                                            {
                                                var ub = new Tbluserbalance
                                                {
                                                    UserId = userData.Id,
                                                    UserName = userData.Name + "-" + userData.Phone,
                                                    OldBal = currentBalance,
                                                    Amount = model.Amount,
                                                    NewBal = Newbal,
                                                    TxnType = "AEPS_CASH_WITHDRAWAL",
                                                    CrdrType = "CR", // Option B => credit
                                                    Remarks = $"AEPS Withdrawal TXN:{tx.TxnId}",
                                                    WlId = userData.Wlid,
                                                    Txndate = DateTime.Now,
                                                    TxnAmount = model.Amount,
                                                    SurCom = rtComm,
                                                    Tds = tds
                                                };

                                                await _context.Tbluserbalances.AddAsync(ub, cancellationToken);
                                                await _context.SaveChangesAsync(cancellationToken);
                                            }
                                        }
                                        catch (Exception ubEx)
                                        {
                                            await LogApiAsync(apiUrl, "DB", "33", ubEx.Message, deviceInfoJson+", x-traceid:"+ traceId + "", requestBody, responseContent, "AEPS", "CashWithdrawal_TblUserBalance");
                                        }
                                    }
                                }
                                catch { /* ignore db update error */ }
                                parsed.Success = parsed.ResponseMessage == "SUCCESS" && parsed.ResponseCode == "00" ? true : false;
                                parsed.appIdentifierToken = appIdToken;
                                parsed.accessToken = accessToken;
                                return parsed;
                            }
                            catch (Exception parseEx)
                            {
                                // return best-effort success wrapper (map message)
                                return new CashWithdrawalResponseDto
                                {
                                    Success = true,
                                    ResponseCode = "00",
                                    ResponseMessage = "Success but parsing failed: " + parseEx.Message,
                                    ResponseData = null,
                                    accessToken = accessToken,
                                    appIdentifierToken = appIdToken
                                };
                            }
                        }
                    }

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
                        fallbackTokens = await GenerateSessionToken(model.Mobile);
                        if (fallbackTokens == null)
                        {
                            try { tx.Status = "FAILED"; tx.ApiRes = Truncate(RedactSensitive(responseContent), 4000); tx.UpdateDate = DateTime.Now; _context.TransactionDetails.Update(tx); await _context.SaveChangesAsync(cancellationToken); } catch { }
                            return new CashWithdrawalResponseDto
                            {
                                Success = false,
                                ResponseCode = "22",
                                ResponseMessage = "Session expired and refresh failed",
                                ResponseData = null,
                                accessToken = accessToken,
                                appIdentifierToken = appIdToken
                            };
                        }

                        accessToken = fallbackTokens.AccessToken;
                        appIdToken = fallbackTokens.AppIdentifierToken;
                        continue; // retry
                    }

                    // final failure
                    var parsedData = JObject.Parse(responseContent);
                    string errorCode = parsedData["error"]?["code"]?.ToString();
                    string errormsg = parsedData["error"]?["message"]?.ToString();
                    string responseCode = parsedData["responseCode"]?.ToString();
                    string responseMessage = parsedData["responseMessage"]?.ToString();

                    return new CashWithdrawalResponseDto
                    {
                        Success = false,
                        ResponseCode = responseCode ?? errorCode,
                        ResponseMessage = responseMessage ?? errormsg,
                        ResponseData = null,
                        accessToken = accessToken,
                        appIdentifierToken = appIdToken
                    };
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    try { tx.Status = "CANCELLED"; tx.UpdateDate = DateTime.Now; _context.TransactionDetails.Update(tx); await _context.SaveChangesAsync(cancellationToken); } catch { }
                    return new CashWithdrawalResponseDto
                    {
                        Success = false,
                        ResponseCode = "33",
                        ResponseMessage = "Request canceled",
                        ResponseData = null,
                        accessToken = accessToken,
                        appIdentifierToken = appIdToken
                    };
                }
                catch (Exception ex)
                {
                    await LogApiAsync(apiUrl, "POST", "33", ex.Message, deviceInfoJson, requestBody, null, "AEPS", "CashWithdrawal");
                    try { tx.Status = "FAILED"; tx.AdminRemarks = ex.Message; tx.UpdateDate = DateTime.Now; _context.TransactionDetails.Update(tx); await _context.SaveChangesAsync(cancellationToken); } catch { }
                    return new CashWithdrawalResponseDto
                    {
                        Success = false,
                        ResponseCode = "33",
                        ResponseMessage = "Unexpected error: " + ex.Message,
                        ResponseData = null,
                        accessToken = accessToken,
                        appIdentifierToken = appIdToken
                    };
                }
            } // end while

            return new CashWithdrawalResponseDto
            {
                Success = false,
                ResponseCode = "33",
                ResponseMessage = "Unhandled flow",
                ResponseData = null,
                accessToken = accessToken,
                appIdentifierToken = appIdToken
            };
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

            var bioJson = new { Skey = sessionKey, Hmac = hmacValue, Data = dataValue };
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
                deviceInfo = new { dc = dc, dpId = dpId, mc = mc, mi = mi, rdsId = rdsId, rdsVer = rdsVer }
            };

            string json = JsonConvert.SerializeObject(payload, Formatting.None);
            string base64Json = Convert.ToBase64String(Encoding.UTF8.GetBytes(json));
            return base64Json;
        }

        private Task<decimal> GetRTCommission(int planId, int slabId, decimal amount)
    => GetCommissionAsync(planId, slabId, amount, "RT");

        private Task<decimal> GetADCommission(int planId, int slabId, decimal amount)
            => GetCommissionAsync(planId, slabId, amount, "AD");

        private Task<decimal> GetMDCommission(int planId, int slabId, decimal amount)
            => GetCommissionAsync(planId, slabId, amount, "MD");

        private Task<decimal> GetWLCommission(int planId, int slabId, decimal amount)
            => GetCommissionAsync(planId, slabId, amount, "WL");

        private async Task<decimal> GetCommissionAsync(
        int planId, int slabId, decimal amount,
        string shareColumn)
        {
            if (slabId == 0) return 0m;

            var slab = await _context.Tblcommissionslabs
                .Where(x => x.PlanId == Convert.ToString(planId) && x.SlabId == Convert.ToString(slabId))
                .FirstOrDefaultAsync();

            if (slab == null) return 0m;

            decimal share = shareColumn switch
            {
                "RT" => (decimal)slab.Rtshare,
                "AD" => (decimal)slab.Adshare,
                "MD" => (decimal)slab.Mdshare,
                "WL" => (decimal)slab.WlShare,
                _ => 0m
            };

            decimal commission = slab.CommissionType == "RS"
                ? share
                : (amount * share / 100);

            return commission;
        }


    }
}

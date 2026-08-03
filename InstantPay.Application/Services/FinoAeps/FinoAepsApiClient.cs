using InstantPay.Application.Interfaces;
using InstantPay.Application.Interfaces.FinoAeps;
using InstantPay.Infrastructure.Sql.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Net.NetworkInformation;
using System.Security.Cryptography;
using System.Text;

namespace InstantPay.Application.Services.FinoAeps
{
    public class FinoAepsApiClient : IFinoAepsApiClient
    {
        private readonly AppDbContext _context;
        private readonly IHttpClientFactory _httpFactory;
        private readonly IMemoryCache _cache;
        private readonly IInstantPayLogService _logService;
        private readonly ILogger<FinoAepsApiClient> _logger;

        private readonly string _prodAuthKey, _prodMasterKey, _prodTokenDecryptKey;
        private readonly string _prodClientId, _prodUsername, _prodPassword;
        private readonly string _prodBearerUrl, _prodGetEncKeyUrl;
        private readonly string _prodAadharPayUrl, _prodAadharPayEncKey;
        private readonly string _prodEkycUrl;

        private readonly string _uatAuthKey, _uatMasterKey, _uatTokenDecryptKey;
        private readonly string _uatClientId, _uatUsername, _uatPassword;
        private readonly string _uatBearerUrl, _uatGetEncKeyUrl, _uatAadharPayUrl;

        private readonly int _prodBearerCacheMin, _uatBearerCacheMin;

        private const string ProdEncKeyCacheKey = "FINO_Prod_EncKey";
        private const string ProdBearerCacheKey  = "FINO_Prod_Bearer";
        private const string UatBearerCacheKey   = "FINO_Uat_Bearer";
        private const string UatEncKeyCacheKey   = "FINO_Uat_EncKey";

        public string ProdIPAddress { get; }

        public FinoAepsApiClient(
            AppDbContext context, IHttpClientFactory httpFactory,
            IMemoryCache cache, IInstantPayLogService logService,
            IConfiguration config, ILogger<FinoAepsApiClient> logger)
        {
            _context    = context;
            _httpFactory = httpFactory;
            _cache      = cache;
            _logService = logService;
            _logger     = logger;

            var cfg  = config.GetSection("FinoAEPS");
            var prod = cfg.GetSection("Prod");
            var uat  = cfg.GetSection("Uat");

            _prodBearerCacheMin   = cfg.GetValue<int?>("BearerTokenCacheMinutes") ?? 50;
            _uatBearerCacheMin    = cfg.GetValue<int?>("UatBearerTokenCacheMinutes") ?? 15;

            _prodAuthKey         = prod["AuthKey"]         ?? "C1D57687-0D0A-42D2-9DC6-5D6AFB4401A4";
            _prodMasterKey       = prod["MasterKey"]       ?? "982b0d01-b262-4ece-a2a2-45be82212ba1";
            _prodTokenDecryptKey = prod["TokenDecryptKey"] ?? "2E12F827-0C78-4718-A254-49243704EF25";
            _prodClientId        = prod["ClientIdName"]    ?? "INSTANTPAYMENT";
            _prodUsername        = prod["Username"]        ?? "instantpayment";
            _prodPassword        = prod["Password"]        ?? "Instantpayment@2024";
            ProdIPAddress        = prod["IPAddress"]       ?? "223.228.207.108";
            _prodBearerUrl       = prod["BearerTokenUrl"]  ?? "https://fpbs.fino.bank.in/auth/realms/apigateway/protocol/openid-connect/token";
            _prodGetEncKeyUrl    = prod["GetEncKeyUrl"]    ?? "https://fpbs.fino.bank.in/AEPSAPIService/AEPSUIService.svc/ProcessRequest/GetEncKey";
            _prodAadharPayUrl    = prod["AadharPayUrl"]    ?? "https://fpbs.fino.bank.in/AEPSAPIService/AEPSUIService.svc/ProcessRequest/AdharPay";
            _prodAadharPayEncKey = prod["AadharPayEncryptionKey"] ?? "2E12F827-0C78-4718-A254-49243704EF25";
            _prodEkycUrl         = prod["MerchantEkycUrl"]  ?? "https://fpbs.fino.bank.in/EKYCUIServiceRegistration/UIServiceEKYCRegistration.svc/MerchantEKYCRegistration";

            _uatAuthKey         = uat["AuthKey"]         ?? "9d035089-4edf-4019-8761-67c35490e76f";
            _uatMasterKey       = uat["MasterKey"]       ?? "982b0d01-b262-4ece-a2a2-45be82212ba1";
            _uatTokenDecryptKey = uat["TokenDecryptKey"] ?? "680e8aff-6938-4ae1-a197-b981e278069a";
            _uatClientId        = uat["ClientIdName"]    ?? "INSTANTPAYMENT";
            _uatUsername        = uat["Username"]        ?? "instantpayment";
            _uatPassword        = uat["Password"]        ?? "Instantpayment@2024";
            _uatBearerUrl       = uat["BearerTokenUrl"]  ?? "http://103.1.112.205:8025/auth/realms/apigateway/protocol/openid-connect/token";
            _uatGetEncKeyUrl    = uat["GetEncKeyUrl"]    ?? "http://103.1.112.205:8025/AEPSAPIService/AEPSUIService.svc/ProcessRequest/GetEncKey";
            _uatAadharPayUrl    = uat["AadharPayUrl"]    ?? "http://103.1.112.205:8025/AEPSAPIService/AEPSUIService.svc/ProcessRequest/AdharPay";
        }

        // ── PROD POST ─────────────────────────────────────────────────
        public async Task<FinoApiCallResult> PostProdAsync(string url, string bodyJson, CancellationToken ct = default)
        {
            string encKey   = await GetProdEncKeyAsync(ct);
            string bearer   = await GetProdBearerAsync(ct);
            string encAuth  = OpenSSLEncrypt($"{{\"ClientId\": 225,\"AuthKey\": \"{_prodAuthKey}\"}}", _prodMasterKey);
            string encBody  = OpenSSLEncrypt(bodyJson, encKey);

            string raw = await SendAsync(url, encAuth, encBody, bearer, _prodClientId, ct);
            await _logService.AddLogAsync(bodyJson, raw, $"FINO_PROD");

            return await ParseResponseAsync(raw, encKey, ct);
        }

        // ── PROD AADHAAR PAY POST ──────────────────────────────────────
        public async Task<FinoApiCallResult> PostAadharPayProdAsync(string bodyJson, CancellationToken ct = default)
        {
            string encKey = await GetProdEncKeyAsync(ct);
            string bearer = await GetProdBearerAsync(ct);
            string encAuth = OpenSSLEncrypt($"{{\"ClientId\": 225,\"AuthKey\": \"{_prodAuthKey}\"}}", _prodMasterKey);
            string encBody = OpenSSLEncrypt(bodyJson, encKey);

            string raw = await SendAsync(_prodAadharPayUrl, encAuth, encBody, bearer, _prodClientId, ct);
            await _logService.AddLogAsync(bodyJson, raw, $"FINO_PROD_AP");

            return await ParseResponseAsync(raw, encKey, ct);
        }

        public async Task<FinoApiCallResult> PostTransactionEnquiryAsync(string url, string bodyJson, CancellationToken ct = default)
        {
            string encKey = await GetProdEncKeyAsync(ct);
            string bearer = await GetProdBearerAsync(ct);
            string encAuth = OpenSSLEncrypt($"{{\"ClientId\": 225,\"AuthKey\": \"{_prodAuthKey}\"}}", _prodMasterKey);
            string encBody = OpenSSLEncrypt(bodyJson, encKey);

            string raw = await SendAsync(url, encAuth, encBody, bearer, _prodClientId, ct);
            await _logService.AddLogAsync(bodyJson, raw, "FINO_AEPS_ENQUIRY");

            return await ParseTransactionEnquiryResponseAsync(raw, encKey, ct);
        }

        // ── UAT POST (Aadhaar Pay only) ───────────────────────────────
        public async Task<FinoApiCallResult> PostUatAsync(string bodyJson, CancellationToken ct = default)
        {
            string bearer   = await GetUatBearerAsync(ct);
            string encKey   = await GetUatEncKeyAsync(bearer, ct);
            string encAuth  = OpenSSLEncrypt($"{{\"ClientId\":225,\"AuthKey\":\"{_uatAuthKey}\"}}", _uatMasterKey);
            string encBody  = OpenSSLEncrypt(bodyJson, encKey);

            var client = _httpFactory.CreateClient("FINO");
            using var hr = new HttpRequestMessage(HttpMethod.Post, _uatAadharPayUrl);
            hr.Headers.TryAddWithoutValidation("Authentication", encAuth);
            hr.Headers.TryAddWithoutValidation("client_id", _uatClientId);
            hr.Content = new StringContent($"\"{encBody}\"", Encoding.UTF8, "application/json");

            var resp = await client.SendAsync(hr, ct);
            string raw = await resp.Content.ReadAsStringAsync(ct);
            await _logService.AddLogAsync(bodyJson, raw, "FINO_UAT_AP");

            return await ParseResponseAsync(raw, encKey, ct);
        }

        // ── MERCHANT EKYC POST ───────────────────────────────────────────
        public async Task<FinoApiCallResult> PostMerchantEkycAsync(string bodyJson, CancellationToken ct = default)
        {

            
            string encKey   = await GetProdEncKeyAsync(ct);
            string encAuth = OpenSSLEncrypt($"{{\"ClientId\": 225,\"AuthKey\": \"{_prodAuthKey}\"}}", _prodMasterKey);
            string encBody  = OpenSSLEncrypt(bodyJson, encKey);

            var client = _httpFactory.CreateClient("FINO");
            using var hr = new HttpRequestMessage(HttpMethod.Post, _prodEkycUrl);
            hr.Headers.TryAddWithoutValidation("Authentication", encAuth);
            hr.Headers.TryAddWithoutValidation("client_id", _prodClientId);
            hr.Content = new StringContent($"\"{encBody}\"", Encoding.UTF8, "application/json");

            var resp = await client.SendAsync(hr, ct);
            string raw = await resp.Content.ReadAsStringAsync(ct);
            await _logService.AddLogAsync(bodyJson, raw, "FINO_EKYC");

            return ParseEkycResponse(raw);
        }

        // ── EKYC RESPONSE PARSER ─────────────────────────────────────────
        private FinoApiCallResult ParseEkycResponse(string raw)
        {
            JObject outer;
            try { outer = JObject.Parse(raw); }
            catch { return new FinoApiCallResult { IsSuccess = false, MessageString = "Invalid JSON from FINO", RawResponse = raw }; }

            string code = outer["ResponseCode"]?.ToString() ?? "-1";
            string msg  = outer["MessageString"]?.ToString() ?? "Unknown error";

            return new FinoApiCallResult
            {
                IsSuccess = code == "0",
                MessageString = msg,
                DecryptedData = outer,
                RawResponse = raw
            };
        }

        private async Task<FinoApiCallResult> ParseTransactionEnquiryResponseAsync(string raw, string encKey, CancellationToken ct)
        {
            JObject outer;
            try { outer = JObject.Parse(raw); }
            catch { return new FinoApiCallResult { IsSuccess = false, ResponseCode = "-1", MessageString = "Invalid JSON from FINO", RawResponse = raw }; }

            string code = outer["ResponseCode"]?.ToString() ?? "-1";
            string msg = outer["MessageString"]?.ToString() ?? outer["DispalyMessage"]?.ToString() ?? "Unknown error";
            if (code != "0" && code != "00")
                return new FinoApiCallResult { IsSuccess = false, ResponseCode = code, MessageString = msg, RawResponse = raw };

            string encryptedData = outer["ResponseData"]?.ToString() ?? "";
            try
            {
                string decrypted = OpenSSLDecrypt(encryptedData, encKey);
                await _logService.AddLogAsync("ResponseData", decrypted, "FINO_AEPS_ENQUIRY_DECRYPT");
                var data = JObject.Parse(decrypted);
                data["ClientRefID"] ??= outer["ClientRefID"];
                return new FinoApiCallResult
                {
                    IsSuccess = true,
                    ResponseCode = code,
                    MessageString = msg,
                    DecryptedData = data,
                    RawResponse = raw
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "FINO transaction enquiry response decrypt/parse failed");
                return new FinoApiCallResult { IsSuccess = false, ResponseCode = "-1", MessageString = "Response decryption failed", RawResponse = raw };
            }
        }

        // ── RESPONSE PARSER ───────────────────────────────────────────
        private async Task<FinoApiCallResult> ParseResponseAsync(string raw, string encKey, CancellationToken ct)
        {
            JObject outer;
            try { outer = JObject.Parse(raw); }
            catch { return new FinoApiCallResult { IsSuccess = false, IsPending = true, ResponseCode = "-1", MessageString = "Invalid JSON from FINO", RawResponse = raw }; }

            string code = outer["ResponseCode"]?.ToString() ?? "-1";
            string msg  = outer["MessageString"]?.ToString() ?? "Unknown error";

            if (code != "0" && code != "00")
                return new FinoApiCallResult
                {
                    IsSuccess = false,
                    IsPending = code is not "400" and not "401" && IsPendingResponseCode(code),
                    ResponseCode = code,
                    MessageString = msg,
                    RawResponse = raw
                };

            string dataStr   = outer["ResponseData"]?.ToString() ?? "{}";
            JObject dataJson;
            try { dataJson = JObject.Parse(dataStr); }
            catch { return new FinoApiCallResult { IsSuccess = false, IsPending = true, ResponseCode = "-1", MessageString = "Malformed ResponseData", RawResponse = raw }; }

            string clientRes = dataJson["ClientRes"]?.ToString() ?? "";
            JObject inner;
            try
            {
                string decrypted = OpenSSLDecrypt(clientRes, encKey);
                await _logService.AddLogAsync("ClientRes", decrypted, "FINO_DECRYPT");
                inner = JObject.Parse(decrypted);
            }
            catch (JsonException)
            {
                // If decrypted value is not JSON, treat it as a plain string value
                string decrypted = OpenSSLDecrypt(clientRes, encKey);
                inner = new JObject { ["Value"] = decrypted };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ClientRes decrypt/parse failed");
                return new FinoApiCallResult { IsSuccess = false, IsPending = true, ResponseCode = "-1", MessageString = "Response decryption failed", RawResponse = raw };
            }

            return new FinoApiCallResult
            {
                IsSuccess = true,
                ResponseCode = code,
                MessageString = msg,
                DecryptedData = inner,
                RawResponse = raw
            };
        }

        private static bool IsPendingResponseCode(string code)
            => code is "-1" or "500" or "502" or "503" or "504" or "998";

        // ── HTTP SENDER ───────────────────────────────────────────────
        private async Task<string> SendAsync(string url, string encAuth, string encBody, string bearer, string clientId, CancellationToken ct)
        {
            var client = _httpFactory.CreateClient("FINO");
            using var hr = new HttpRequestMessage(HttpMethod.Post, url);
            hr.Headers.TryAddWithoutValidation("Authentication", encAuth);
            hr.Headers.TryAddWithoutValidation("Authorization", $"Bearer {bearer}");
            hr.Headers.TryAddWithoutValidation("client_id", clientId);
            hr.Content = new StringContent($"\"{encBody}\"", Encoding.UTF8, "application/json");
            var resp = await client.SendAsync(hr, ct);
            return await resp.Content.ReadAsStringAsync(ct);
        }

        // ── TOKEN MANAGEMENT ─────────────────────────────────────────
        private async Task<string> GetProdEncKeyAsync(CancellationToken ct)
        {
            if (_cache.TryGetValue(ProdEncKeyCacheKey, out string? cached) && !string.IsNullOrEmpty(cached))
                return cached;

            string? dbKey = await GetProdEncKeyFromDbAsync(ct);
            if (!string.IsNullOrEmpty(dbKey))
            {
                _cache.Set(ProdEncKeyCacheKey, dbKey, TimeSpan.FromMinutes(20));
                return dbKey;
            }

            return await FetchAndCacheProdEncKeyAsync(ct);
        }

        private async Task<string?> GetProdEncKeyFromDbAsync(CancellationToken ct)
        {
            try
            {
                var today = DateTime.Now.Date;
                var row = await _context.Finotokens
                    .Where(f => f.Reqdate >= today && f.Reqdate <= DateTime.Now)
                    .OrderByDescending(f => f.Id)
                    .FirstOrDefaultAsync(ct);
                return row?.TokenKey;
            }
            catch (Exception ex) { _logger.LogWarning(ex, "ProdEncKey DB read failed"); return null; }
        }

        private async Task<string> FetchAndCacheProdEncKeyAsync(CancellationToken ct)
        {
            string bearer   = await GetProdBearerAsync(ct);
            string encAuth = OpenSSLEncrypt($"{{\"ClientId\": 225,\"AuthKey\": \"{_prodAuthKey}\"}}", _prodMasterKey);
            var client = _httpFactory.CreateClient("FINO");
            using var hr = new HttpRequestMessage(HttpMethod.Post, _prodGetEncKeyUrl);
            hr.Headers.TryAddWithoutValidation("Authentication", encAuth);
            hr.Headers.TryAddWithoutValidation("Authorization", $"Bearer {bearer}");
            hr.Content = new StringContent("", Encoding.UTF8, "application/x-www-form-urlencoded");

            var resp = await client.SendAsync(hr, ct);
            string raw = await resp.Content.ReadAsStringAsync(ct);

            var j = JObject.Parse(raw);
            if (j["ResponseCode"]?.ToString() != "0")
                throw new Exception($"FINO GetEncKey failed: {raw}");

            string responseData = j["ResponseData"]?.ToString()
                ?? throw new Exception($"FINO GetEncKey returned no ResponseData: {raw}");

            string decrypted = OpenSSLDecrypt(responseData, _prodTokenDecryptKey);
            var innerJson = JObject.Parse(decrypted);
            string key = innerJson["EncrytionKey"]?.ToString() ?? innerJson["EncryptionKey"]?.ToString()
                ?? throw new Exception($"FINO GetEncKey inner JSON has no key: {decrypted}");

            _cache.Set(ProdEncKeyCacheKey, key, TimeSpan.FromMinutes(20));
            try
            {
                _context.Finotokens.Add(new Finotoken { TokenKey = key, Reqdate = DateTime.Now });
                await _context.SaveChangesAsync(ct);
            }
            catch (Exception ex) { _logger.LogWarning(ex, "ProdEncKey DB store failed"); }
            return key;
        }

        private async Task<string> GetProdBearerAsync(CancellationToken ct)
        {
            if (_cache.TryGetValue(ProdBearerCacheKey, out string? cached) && !string.IsNullOrEmpty(cached))
                return cached;

            string encAuth = OpenSSLEncrypt($"{{\"ClientId\": 225,\"AuthKey\": \"{_prodAuthKey}\"}}", _prodMasterKey);
            var client = _httpFactory.CreateClient("FINO");
            using var hr = new HttpRequestMessage(HttpMethod.Post, _prodBearerUrl);
            hr.Headers.TryAddWithoutValidation("Authentication", encAuth);
            hr.Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "password",
                ["client_id"]  = _prodClientId,
                ["username"]   = _prodUsername,
                ["password"]   = _prodPassword
            });

            var resp  = await client.SendAsync(hr, ct);
            string raw = await resp.Content.ReadAsStringAsync(ct);
            string token = JObject.Parse(raw)["access_token"]?.ToString()
                           ?? throw new Exception($"FINO prod bearer failed: {raw}");

            _cache.Set(ProdBearerCacheKey, token, TimeSpan.FromMinutes(_prodBearerCacheMin));
            return token;
        }

        private async Task<string> GetUatBearerAsync(CancellationToken ct)
        {
            if (_cache.TryGetValue(UatBearerCacheKey, out string? cached) && !string.IsNullOrEmpty(cached))
                return cached;

            string encAuth = OpenSSLEncrypt($"{{\"ClientId\": 225,\"AuthKey\": \"{_uatAuthKey}\"}}", _uatMasterKey);
            var client = _httpFactory.CreateClient("FINO");
            using var hr = new HttpRequestMessage(HttpMethod.Post, _uatBearerUrl);
            hr.Headers.TryAddWithoutValidation("Authentication", encAuth);
            hr.Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "password",
                ["client_id"]  = _uatClientId,
                ["username"]   = _uatUsername,
                ["password"]   = _uatPassword
            });

            var resp  = await client.SendAsync(hr, ct);
            string raw = await resp.Content.ReadAsStringAsync(ct);
            string token = JObject.Parse(raw)["access_token"]?.ToString()
                           ?? throw new Exception($"FINO UAT bearer failed: {raw}");

            _cache.Set(UatBearerCacheKey, token, TimeSpan.FromMinutes(_uatBearerCacheMin));
            return token;
        }

        private async Task<string> GetUatEncKeyAsync(string uatBearer, CancellationToken ct)
        {
            if (_cache.TryGetValue(UatEncKeyCacheKey, out string? cached) && !string.IsNullOrEmpty(cached))
                return cached;

            string encAuth = OpenSSLEncrypt($"{{\"ClientId\": 225,\"AuthKey\": \"{_uatAuthKey}\"}}", _uatMasterKey);
            var client = _httpFactory.CreateClient("FINO");
            using var hr = new HttpRequestMessage(HttpMethod.Post, _uatGetEncKeyUrl);
            hr.Headers.TryAddWithoutValidation("Authentication", encAuth);
            hr.Headers.TryAddWithoutValidation("Authorization", $"Bearer {uatBearer}");
            hr.Content = new StringContent("", Encoding.UTF8, "application/x-www-form-urlencoded");

            var resp  = await client.SendAsync(hr, ct);
            string raw = await resp.Content.ReadAsStringAsync(ct);
            var j = JObject.Parse(raw);
            string enc = j["EncrytionKey"]?.ToString() ?? j["EncryptionKey"]?.ToString()
                         ?? throw new Exception($"FINO UAT GetEncKey failed: {raw}");

            string key = OpenSSLDecrypt(enc, _uatTokenDecryptKey);
            _cache.Set(UatEncKeyCacheKey, key, TimeSpan.FromMinutes(_uatBearerCacheMin));
            return key;
        }

        // ── CRYPTO HELPERS ────────────────────────────────────────────
        internal static string OpenSSLEncrypt(string plainText, string passphrase)
        {
            byte[] salt = RandomNumberGenerator.GetBytes(8);
            DeriveKeyAndIv(Encoding.UTF8.GetBytes(passphrase), salt, out byte[] key, out byte[] iv);
            using var aes = Aes.Create();
            aes.Key = key; aes.IV = iv; aes.Mode = CipherMode.CBC; aes.Padding = PaddingMode.PKCS7;
            byte[] cipher = aes.CreateEncryptor().TransformFinalBlock(
                Encoding.UTF8.GetBytes(plainText), 0, Encoding.UTF8.GetByteCount(plainText));
            byte[] result = new byte[16 + cipher.Length];
            Encoding.ASCII.GetBytes("Salted__").CopyTo(result, 0);
            salt.CopyTo(result, 8);
            cipher.CopyTo(result, 16);
            return Convert.ToBase64String(result);
        }

        internal static string OpenSSLDecrypt(string cipherB64, string passphrase)
        {
            byte[] data = Convert.FromBase64String(cipherB64);
            byte[] salt = data[8..16];
            DeriveKeyAndIv(Encoding.UTF8.GetBytes(passphrase), salt, out byte[] key, out byte[] iv);
            using var aes = Aes.Create();
            aes.Key = key; aes.IV = iv; aes.Mode = CipherMode.CBC; aes.Padding = PaddingMode.PKCS7;
            byte[] plain = aes.CreateDecryptor().TransformFinalBlock(data, 16, data.Length - 16);
            return Encoding.UTF8.GetString(plain);
        }

        private static void DeriveKeyAndIv(byte[] pass, byte[] salt, out byte[] key, out byte[] iv)
        {
            byte[] d0 = MD5.HashData(Concat(pass, salt));
            byte[] d1 = MD5.HashData(Concat(d0, pass, salt));
            byte[] d2 = MD5.HashData(Concat(d1, pass, salt));
            key = new byte[32]; iv = new byte[16];
            d0.CopyTo(key, 0); d1.CopyTo(key, 16); d2.CopyTo(iv, 0);
        }

        private static byte[] Concat(params byte[][] arrays)
        {
            byte[] r = new byte[arrays.Sum(a => a.Length)];
            int off = 0;
            foreach (var a in arrays) { a.CopyTo(r, off); off += a.Length; }
            return r;
        }

        // ── PUBLIC HELPERS ────────────────────────────────────────────
        public string DecryptSessionKey(string sessionKeyBase64)
        {
            byte[] md5Key = MD5.HashData(Encoding.UTF8.GetBytes("MrChandan"));
            using var tdes = TripleDES.Create();
            tdes.Key = md5Key; tdes.Mode = CipherMode.ECB; tdes.Padding = PaddingMode.PKCS7;
            byte[] cipher = Convert.FromBase64String(sessionKeyBase64);
            byte[] plain  = tdes.CreateDecryptor().TransformFinalBlock(cipher, 0, cipher.Length);
            return Encoding.UTF8.GetString(plain);
        }

        public string ComputeChecksum(string raw)
        {
            byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
            return Convert.ToHexString(hash).ToLower();
        }

        public string GenerateTxnId()
            => "FAE" + DateTime.Now.ToString("yyyyMMddHHmmssfff") + Guid.NewGuid().ToString("N")[..3].ToUpper();

        public string GetMacAddress()
        {
            try
            {
                var nic = NetworkInterface.GetAllNetworkInterfaces()
                    .FirstOrDefault(n => n.OperationalStatus == OperationalStatus.Up
                        && n.NetworkInterfaceType != NetworkInterfaceType.Loopback);
                return nic?.GetPhysicalAddress().ToString() ?? "000000000000";
            }
            catch { return "000000000000"; }
        }
    }
}

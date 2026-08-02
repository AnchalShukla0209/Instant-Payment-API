using InstantPay.Application.DTOs;
using InstantPay.Application.Interfaces.PPI;
using InstantPay.Infrastructure.Sql.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;

namespace InstantPay.Application.Services.PPI;

public class PPIFundTransferService : IPPIFundTransferService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<PPIFundTransferService> _logger;
    private readonly string _baseUrl;
    private readonly string _appId;
    private readonly string _authKey;
    private readonly string _secretKey;

    private const decimal MinAmount = 100m;
    private const decimal MaxAmount = 100000m;
    private readonly AppDbContext _context;

    public PPIFundTransferService(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<PPIFundTransferService> logger,
        AppDbContext context)
    {
        _httpClient = httpClientFactory.CreateClient();
        _logger = logger;

        var ppiConfig = configuration.GetSection("PPI");
        _baseUrl = ppiConfig["BaseUrl"] ?? "https://api.digikhata.in/p2a/";
        _appId = ppiConfig["AppId"] ?? string.Empty;
        _authKey = ppiConfig["AuthKey"] ?? string.Empty;
        _secretKey = ppiConfig["SecretKey"] ?? string.Empty;

        _httpClient.Timeout = TimeSpan.FromSeconds(30);
        _context = context;
    }

    private HttpRequestMessage CreatePpiRequest(string endpoint, HttpContent content, string bearerToken)
    {
        var req = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}{endpoint}") { Content = content };
        req.Headers.Add("AppID",    _appId);
        req.Headers.Add("AuthKey",  _authKey);
        req.Headers.Add("SecretKey", _secretKey);
        if (!string.IsNullOrEmpty(bearerToken))
            req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", bearerToken);
        return req;
    }

    public async Task<PPIFundTransferOtpResponse> GetOtpAsync(PPIFundTransferOtpRequest request)
    {
        try
        {
            if (request.APIKey != "PPI01")
                return Fail("Invalid API Key");

            if (!decimal.TryParse(request.Amount, out decimal amount))
                return Fail("Invalid amount format");

            if (amount < MinAmount || amount > MaxAmount)
                return Fail($"Please Enter Amount between {MinAmount:0} to {MaxAmount:0}");

            var payload = new
            {
                mobilenumber = request.MobileNumber,
                bankaccountnumber = request.BankAccountNumber,
                ifsccode = request.IFSCCode,
                beneficiaryid = request.BeneficiaryId,
                amount = request.Amount
            };

            var content = new StringContent(
                JsonSerializer.Serialize(payload),
                Encoding.UTF8,
                "application/json");

            _logger.LogInformation("PPI FundTransfer GetOtp | Mobile: {Mobile} BeneId: {BeneId} Amount: {Amount}",
                request.MobileNumber, request.BeneficiaryId, request.Amount);

            using var httpReq = CreatePpiRequest("v2/fundtransfer/getotp", content, request.TokeyKey);
            var response = await _httpClient.SendAsync(httpReq);
            var responseContent = await response.Content.ReadAsStringAsync();

            _logger.LogInformation("PPI FundTransfer GetOtp Response: {Response}", responseContent);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("PPI FundTransfer GetOtp HTTP error {StatusCode}", response.StatusCode);
                var log = new Apilog
                {
                    Apiname = "PPI-FundTransferOTP",
                    Reqdatae = DateTime.Now,
                    Request = JsonSerializer.Serialize(payload),
                    Response = responseContent + "||" + response.StatusCode
                };
                _context.Apilogs.Add(log);
                await _context.SaveChangesAsync();
                return Fail("Failed to send OTP. Please try again later.");
            }

            var logs = new Apilog
            {
                Apiname = "PPI-FundTransferOTP",
                Reqdatae = DateTime.Now,
                Request = JsonSerializer.Serialize(payload),
                Response = responseContent + "||" + response.StatusCode
            };
            _context.Apilogs.Add(logs);
            await _context.SaveChangesAsync();


            using var jsonDoc = JsonDocument.Parse(responseContent);
            var root = jsonDoc.RootElement;

            if (root.TryGetProperty("resultCode", out var resultCode) &&
                root.TryGetProperty("resultMessage", out var resultMessage))
            {
                bool success = resultCode.GetRawText().Trim('"') == "2000";
                string otpToken = string.Empty;

                if (success && root.TryGetProperty("result", out var result))
                    otpToken = result.TryGetProperty("otpToken", out var t) ? t.GetString() ?? "" : "";

                return new PPIFundTransferOtpResponse
                {
                    Status_Code = success ? "1" : "0",
                    Message = resultMessage.GetString() ?? (success ? "OTP sent successfully" : "Failed to send OTP"),
                    Data = otpToken
                };
            }

            _logger.LogWarning("Unexpected PPI FundTransfer GetOtp response: {Response}", responseContent);
            return Fail("Unexpected response from OTP service");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in PPI FundTransfer GetOtp");
            return Fail("An unexpected error occurred.");
        }
    }

    private static PPIFundTransferOtpResponse Fail(string message) =>
        new() { Status_Code = "0", Message = message, Data = "" };
}

using System.Text;
using System.Text.Json;
using InstantPay.Application.DTOs;
using InstantPay.Application.Interfaces.PPI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

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

    public PPIFundTransferService(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<PPIFundTransferService> logger)
    {
        _httpClient = httpClientFactory.CreateClient();
        _logger = logger;

        var ppiConfig = configuration.GetSection("PPI");
        _baseUrl = ppiConfig["BaseUrl"] ?? "https://api.digikhata.in/p2a/";
        _appId = ppiConfig["AppId"] ?? string.Empty;
        _authKey = ppiConfig["AuthKey"] ?? string.Empty;
        _secretKey = ppiConfig["SecretKey"] ?? string.Empty;

        _httpClient.Timeout = TimeSpan.FromSeconds(30);
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

            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("AppID", _appId);
            _httpClient.DefaultRequestHeaders.Add("AuthKey", _authKey);
            _httpClient.DefaultRequestHeaders.Add("SecretKey", _secretKey);
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {request.TokeyKey}");

            _logger.LogInformation("PPI FundTransfer GetOtp | Mobile: {Mobile} BeneId: {BeneId} Amount: {Amount}",
                request.MobileNumber, request.BeneficiaryId, request.Amount);

            var response = await _httpClient.PostAsync($"{_baseUrl}v2/fundtransfer/getotp", content);
            var responseContent = await response.Content.ReadAsStringAsync();

            _logger.LogInformation("PPI FundTransfer GetOtp Response: {Response}", responseContent);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("PPI FundTransfer GetOtp HTTP error {StatusCode}", response.StatusCode);
                return Fail("Failed to send OTP. Please try again later.");
            }

            using var jsonDoc = JsonDocument.Parse(responseContent);
            var root = jsonDoc.RootElement;

            if (root.TryGetProperty("resultCode", out var resultCode) &&
                root.TryGetProperty("resultMessage", out var resultMessage))
            {
                bool success = resultCode.GetString() == "2000";
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

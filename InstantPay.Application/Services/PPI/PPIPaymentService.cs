using System.Text;
using System.Text.Json;
using InstantPay.Application.DTOs;
using InstantPay.Application.Interfaces.PPI;
using InstantPay.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace InstantPay.Application.Services.PPI;

public class PPIPaymentService : IPPIPaymentService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<PPIPaymentService> _logger;
    private readonly IWalletService _walletService;
    private readonly string _baseUrl;
    private readonly string _appId;
    private readonly string _authKey;
    private readonly string _secretKey;

    public PPIPaymentService(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<PPIPaymentService> logger,
        IWalletService walletService)
    {
        _httpClient = httpClientFactory.CreateClient();
        _configuration = configuration;
        _logger = logger;
        _walletService = walletService;

        // Load PPI configuration from appsettings
        var ppiConfig = configuration.GetSection("PPI");
        _baseUrl = ppiConfig["BaseUrl"] ?? "https://api.digikhata.in/p2a/";
        _appId = ppiConfig["AppId"] ?? "INSTANTPAYMENT";
        _authKey = ppiConfig["AuthKey"] ?? string.Empty;
        _secretKey = ppiConfig["SecretKey"] ?? string.Empty;

        _httpClient.Timeout = TimeSpan.FromSeconds(30);
    }

    public async Task<PPISendPaymentOtpResponse> SendPaymentOtpAsync(PPISendPaymentOtpRequest request)
    {
        try
        {
            // Validate API Key
            if (request.APIKey != "PPI01")
            {
                _logger.LogWarning("Invalid API Key provided for PPI Send Payment OTP");
                return new PPISendPaymentOtpResponse
                {
                    Status_Code = "0",
                    Message = "Invalid API Key",
                    Data = ""
                };
            }

            // Validate amount range
            if (decimal.TryParse(request.Amount, out decimal amount))
            {
                if (amount < 100 || amount > 50000)
                {
                    _logger.LogWarning("Invalid amount {Amount} for PPI Send Payment OTP. Amount must be between 100 and 50000", amount);
                    return new PPISendPaymentOtpResponse
                    {
                        Status_Code = "0",
                        Message = "Please Enter Amount between 100 to 50000",
                        Data = ""
                    };
                }
            }
            else
            {
                _logger.LogWarning("Invalid amount format: {Amount}", request.Amount);
                return new PPISendPaymentOtpResponse
                {
                    Status_Code = "0",
                    Message = "Invalid amount format",
                    Data = ""
                };
            }

            // Prepare the request payload for PPI API
            var payload = new
            {
                mobilenumber = request.SenderMobile,
                bankaccountnumber = request.AccountNo,
                ifsccode = request.Ifsccode,
                beneficiaryid = request.BeneId,
                amount = request.Amount
            };

            var jsonPayload = JsonSerializer.Serialize(payload);
            var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

            _logger.LogInformation("Sending PPI Send Payment OTP request for Mobile: {Mobile}, BeneId: {BeneId}, Amount: {Amount}", 
                request.SenderMobile, request.BeneId, request.Amount);

            // Add headers
            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("AppID", _appId);
            _httpClient.DefaultRequestHeaders.Add("AuthKey", _authKey);
            _httpClient.DefaultRequestHeaders.Add("SecretKey", _secretKey);
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {request.TokeyKey}");

            // Make the API call
            var response = await _httpClient.PostAsync($"{_baseUrl}v2/fundtransfer/getotp", content);
            
            var responseContent = await response.Content.ReadAsStringAsync();
            _logger.LogInformation("PPI Send Payment OTP API Response: {Response}", responseContent);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("PPI Send Payment OTP API returned error status: {StatusCode}, Response: {Response}", 
                    response.StatusCode, responseContent);
                
                return new PPISendPaymentOtpResponse
                {
                    Status_Code = "0",
                    Message = "Failed to send payment OTP. Please try again later.",
                    Data = ""
                };
            }

            // Parse the response
            using var jsonDoc = JsonDocument.Parse(responseContent);
            var root = jsonDoc.RootElement;

            // Check if the response has the expected structure
            if (root.TryGetProperty("resultCode", out var resultCode) && 
                root.TryGetProperty("resultMessage", out var resultMessage))
            {
                if (resultCode.GetString() == "2000" && root.TryGetProperty("result", out var result))
                {
                    string otpToken = result.TryGetProperty("otpToken", out var oToken) ? oToken.GetString() ?? "" : "";
                    string message = resultMessage.GetString() ?? "OTP sent successfully";

                    return new PPISendPaymentOtpResponse
                    {
                        Status_Code = "1",
                        Message = message,
                        Data = otpToken
                    };
                }
                else
                {
                    return new PPISendPaymentOtpResponse
                    {
                        Status_Code = "0",
                        Message = resultMessage.GetString() ?? "Failed to send payment OTP",
                        Data = ""
                    };
                }
            }

            // Fallback for unexpected response format
            _logger.LogWarning("Unexpected PPI Send Payment OTP API response format: {Response}", responseContent);
            return new PPISendPaymentOtpResponse
            {
                Status_Code = "0",
                Message = "Unexpected response from payment OTP service",
                Data = ""
            };
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error occurred while calling PPI Send Payment OTP API");
            return new PPISendPaymentOtpResponse
            {
                Status_Code = "0",
                Message = "Network error. Please check your connection.",
                Data = ""
            };
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogError(ex, "Request timeout while calling PPI Send Payment OTP API");
            return new PPISendPaymentOtpResponse
            {
                Status_Code = "0",
                Message = "Request timeout. Please try again.",
                Data = ""
            };
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Error parsing PPI Send Payment OTP API response");
            return new PPISendPaymentOtpResponse
            {
                Status_Code = "0",
                Message = "Error processing response.",
                Data = ""
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in PPI Send Payment OTP");
            return new PPISendPaymentOtpResponse
            {
                Status_Code = "0",
                Message = "An unexpected error occurred.",
                Data = ""
            };
        }
    }
}

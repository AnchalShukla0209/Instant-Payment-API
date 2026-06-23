using System.Text;
using System.Text.Json;
using InstantPay.Application.DTOs;
using InstantPay.Application.Interfaces.PPI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace InstantPay.Application.Services.PPI;

public class PPIOtpService : IPPIOtpService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<PPIOtpService> _logger;
    private readonly string _baseUrl;
    private readonly string _appId;
    private readonly string _authKey;
    private readonly string _secretKey;

    public PPIOtpService(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<PPIOtpService> logger)
    {
        _httpClient = httpClientFactory.CreateClient();
        _configuration = configuration;
        _logger = logger;

        // Load PPI configuration from appsettings
        var ppiConfig = configuration.GetSection("PPI");
        _baseUrl = ppiConfig["BaseUrl"] ?? "https://api.digikhata.in/p2a/";
        _appId = ppiConfig["AppId"] ?? "INSTANTPAYMENT";
        _authKey = ppiConfig["AuthKey"] ?? "b]%oIAL#EEtdb9?}|]t71>u.=Cv=9SM|eBw<xV@2HNIUCdO()j";
        _secretKey = ppiConfig["SecretKey"] ?? "v&.5zef-4FrbD[;2/aMCe6N|zo{a;s]%DZ8h>!oR1^36K*KVcm";

        _httpClient.Timeout = TimeSpan.FromSeconds(30);
    }

    public async Task<GeneratePPIOtpResponse> GenerateOtpAsync(GeneratePPIOtpRequest request)
    {
        try
        {
            // Validate API Key
            if (request.APIKey != "PPI01")
            {
                _logger.LogWarning("Invalid API Key provided for PPI OTP generation");
                return new GeneratePPIOtpResponse
                {
                    Status_Code = "0",
                    Message = "Invalid API Key",
                    Data = ""
                };
            }

            // Prepare the request payload for PPI API
            var payload = new
            {
                walletaccreatorcode = request.UserId,
                walletaccreatorpincode = request.Pincode,
                walletaccreatorname = request.RTName,
                mobilenumber = request.SenderMobile
            };

            var jsonPayload = JsonSerializer.Serialize(payload);
            var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

            _logger.LogInformation("Sending PPI OTP generation request for UserId: {UserId}, Mobile: {Mobile}", 
                request.UserId, request.SenderMobile);

            // Add headers
            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("AppID", _appId);
            _httpClient.DefaultRequestHeaders.Add("AuthKey", _authKey);
            _httpClient.DefaultRequestHeaders.Add("SecretKey", _secretKey);

            // Make the API call
            var response = await _httpClient.PostAsync($"{_baseUrl}v1/generateotp", content);
            
            var responseContent = await response.Content.ReadAsStringAsync();
            _logger.LogInformation("PPI API Response: {Response}", responseContent);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("PPI API returned error status: {StatusCode}, Response: {Response}", 
                    response.StatusCode, responseContent);
                
                return new GeneratePPIOtpResponse
                {
                    Status_Code = "0",
                    Message = "Failed to generate OTP. Please try again later.",
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
                    if (result.TryGetProperty("otpToken", out var otpToken))
                    {
                        return new GeneratePPIOtpResponse
                        {
                            Status_Code = "1",
                            Message = resultMessage.GetString() ?? "OTP Sent Successfully",
                            Data = otpToken.GetString() ?? ""
                        };
                    }
                }
                else
                {
                    return new GeneratePPIOtpResponse
                    {
                        Status_Code = "0",
                        Message = resultMessage.GetString() ?? "Sender not registered",
                        Data = ""
                    };
                }
            }

            // Fallback for unexpected response format
            _logger.LogWarning("Unexpected PPI API response format: {Response}", responseContent);
            return new GeneratePPIOtpResponse
            {
                Status_Code = "0",
                Message = "Unexpected response from OTP service",
                Data = ""
            };
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error occurred while calling PPI API");
            return new GeneratePPIOtpResponse
            {
                Status_Code = "0",
                Message = "Network error. Please check your connection.",
                Data = ""
            };
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogError(ex, "Request timeout while calling PPI API");
            return new GeneratePPIOtpResponse
            {
                Status_Code = "0",
                Message = "Request timeout. Please try again.",
                Data = ""
            };
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Error parsing PPI API response");
            return new GeneratePPIOtpResponse
            {
                Status_Code = "0",
                Message = "Error processing response.",
                Data = ""
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in PPI OTP generation");
            return new GeneratePPIOtpResponse
            {
                Status_Code = "0",
                Message = "An unexpected error occurred.",
                Data = ""
            };
        }
    }

    public async Task<VerifyPPIOtpResponse> VerifyOtpAsync(VerifyPPIOtpRequest request)
    {
        try
        {
            // Validate API Key
            if (request.APIKey != "PPI01")
            {
                _logger.LogWarning("Invalid API Key provided for PPI OTP verification");
                return new VerifyPPIOtpResponse
                {
                    Status_Code = "0",
                    Message = "Invalid API Key",
                    Data = new List<PPIWalletDetail>()
                };
            }

            // Prepare the request payload for PPI API
            var payload = new
            {
                otpToken = request.OTPToken,
                otp = request.OTP
            };

            var jsonPayload = JsonSerializer.Serialize(payload);
            var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("AppID", _appId);
            _httpClient.DefaultRequestHeaders.Add("AuthKey", _authKey);
            _httpClient.DefaultRequestHeaders.Add("SecretKey", _secretKey);
            var response = await _httpClient.PostAsync($"{_baseUrl}v1/verifyotp", content);
            var responseContent = await response.Content.ReadAsStringAsync();
            _logger.LogInformation("PPI Verify API Response: {Response}", responseContent);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("PPI Verify API returned error status: {StatusCode}, Response: {Response}", 
                    response.StatusCode, responseContent);
                
                return new VerifyPPIOtpResponse
                {
                    Status_Code = "0",
                    Message = "Failed to verify OTP. Please try again later.",
                    Data = new List<PPIWalletDetail>()
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
                    var walletDetail = new PPIWalletDetail
                    {
                        SenderName = result.TryGetProperty("walletHolderName", out var holderName) ? holderName.GetString() ?? "" : "",
                        WalletStatus = result.TryGetProperty("walletAcOpened", out var acOpened) ? acOpened.GetString() ?? "" : "",
                        TokeyKey = result.TryGetProperty("token", out var token) ? token.GetString() ?? "" : "",
                        ApplicationNumber = result.TryGetProperty("walletAcApplicationNumber", out var appNumber) ? appNumber.GetString() ?? "" : "",
                        WalletLimit = result.TryGetProperty("walletToBankLimitAvailable", out var limit) ? limit.GetString() ?? "" : "",
                        walletCurrentBalance = result.TryGetProperty("walletCurrentBalance", out var balance) ? balance.GetString() ?? "" : ""
                    };

                    return new VerifyPPIOtpResponse
                    {
                        Status_Code = "1",
                        Message = resultMessage.GetString() ?? "OTP Verified",
                        Data = new List<PPIWalletDetail> { walletDetail }
                    };
                }
                else
                {
                    return new VerifyPPIOtpResponse
                    {
                        Status_Code = "0",
                        Message = resultMessage.GetString() ?? "OTP verification failed",
                        Data = new List<PPIWalletDetail>()
                    };
                }
            }

            // Fallback for unexpected response format
            _logger.LogWarning("Unexpected PPI Verify API response format: {Response}", responseContent);
            return new VerifyPPIOtpResponse
            {
                Status_Code = "0",
                Message = "Unexpected response from OTP verification service",
                Data = new List<PPIWalletDetail>()
            };
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error occurred while calling PPI Verify API");
            return new VerifyPPIOtpResponse
            {
                Status_Code = "0",
                Message = "Network error. Please check your connection.",
                Data = new List<PPIWalletDetail>()
            };
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogError(ex, "Request timeout while calling PPI Verify API");
            return new VerifyPPIOtpResponse
            {
                Status_Code = "0",
                Message = "Request timeout. Please try again.",
                Data = new List<PPIWalletDetail>()
            };
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Error parsing PPI Verify API response");
            return new VerifyPPIOtpResponse
            {
                Status_Code = "0",
                Message = "Error processing response.",
                Data = new List<PPIWalletDetail>()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in PPI OTP verification");
            return new VerifyPPIOtpResponse
            {
                Status_Code = "0",
                Message = "An unexpected error occurred.",
                Data = new List<PPIWalletDetail>()
            };
        }
    }
}

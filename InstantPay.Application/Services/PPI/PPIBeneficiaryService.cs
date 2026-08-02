using System.Text;
using System.Text.Json;
using InstantPay.Application.DTOs;
using InstantPay.Application.Interfaces.PPI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace InstantPay.Application.Services.PPI;

public class PPIBeneficiaryService : IPPIBeneficiaryService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<PPIBeneficiaryService> _logger;
    private readonly string _baseUrl;
    private readonly string _appId;
    private readonly string _authKey;
    private readonly string _secretKey;

    public PPIBeneficiaryService(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<PPIBeneficiaryService> logger)
    {
        _httpClient = httpClientFactory.CreateClient();
        _logger = logger;

        // Load PPI configuration from appsettings
        var ppiConfig = configuration.GetSection("PPI");
        _baseUrl = ppiConfig["BaseUrl"] ?? "https://api.digikhata.in/p2a/";
        _appId = ppiConfig["AppId"] ?? "INSTANTPAYMENT";
        _authKey = ppiConfig["AuthKey"] ?? string.Empty;
        _secretKey = ppiConfig["SecretKey"] ?? string.Empty;

        _httpClient.Timeout = TimeSpan.FromSeconds(30);
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

    public async Task<PPIBeneListResponse> GetBeneficiaryListAsync(PPIBeneListRequest request)
    {
        try
        {
            // Validate API Key
            if (request.APIKey != "PPI01")
            {
                _logger.LogWarning("Invalid API Key provided for PPI Beneficiary List");
                return new PPIBeneListResponse
                {
                    Status_Code = "0",
                    Message = "Invalid API Key",
                    Data = new List<PPIBeneficiary>()
                };
            }

            // Prepare the request payload for PPI API
            var payload = new
            {
                mobilenumber = request.SenderMobile
            };

            var jsonPayload = JsonSerializer.Serialize(payload);
            var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

            _logger.LogInformation("Sending PPI Beneficiary List request for Mobile: {Mobile}", request.SenderMobile);

            // Make the API call
            using var httpReq = CreatePpiRequest("v1/beneficiary/list", content, request.TokeyKey);
            var response = await _httpClient.SendAsync(httpReq);
            
            var responseContent = await response.Content.ReadAsStringAsync();
            _logger.LogInformation("PPI Beneficiary List API Response: {Response}", responseContent);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("PPI Beneficiary List API returned error status: {StatusCode}, Response: {Response}", 
                    response.StatusCode, responseContent);
                
                return new PPIBeneListResponse
                {
                    Status_Code = "0",
                    Message = "Failed to get beneficiary list. Please try again later.",
                    Data = new List<PPIBeneficiary>()
                };
            }

            // Parse the response
            using var jsonDoc = JsonDocument.Parse(responseContent);
            var root = jsonDoc.RootElement;

            // Check if the response has the expected structure
            if (root.TryGetProperty("resultCode", out var resultCode) && 
                root.TryGetProperty("resultMessage", out var resultMessage))
            {
                if (resultCode.GetRawText().Trim('"') == "2000" && root.TryGetProperty("result", out var result))
                {
                    var beneficiaries = new List<PPIBeneficiary>();

                    if (result.TryGetProperty("beneficiaries", out var beneArray) && beneArray.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var bene in beneArray.EnumerateArray())
                        {
                            beneficiaries.Add(new PPIBeneficiary
                            {
                                beneId = bene.TryGetProperty("beneId", out var beneId) ? beneId.GetInt32() : 0,
                                beneficiaryMobile = bene.TryGetProperty("beneficiaryMobile", out var mobile) ? mobile.GetString() ?? "" : "",
                                beneficiaryName = bene.TryGetProperty("beneficiaryName", out var name) ? name.GetString() ?? "" : "",
                                ifsCcode = bene.TryGetProperty("ifsCcode", out var ifsc) ? ifsc.GetString() ?? "" : "",
                                accountNo = bene.TryGetProperty("accountNo", out var accNo) ? accNo.GetString() ?? "" : "",
                                bankid = bene.TryGetProperty("bankid", out var bankId) ? bankId.GetInt32() : 0,
                                bank = bene.TryGetProperty("bank", out var bank) ? bank.GetString() ?? "" : "",
                                isAcValidate = bene.TryGetProperty("isAcValidate", out var acValidate) ? acValidate.GetInt32() : 0,
                                is_otp_required = bene.TryGetProperty("is_otp_required", out var otpReq) ? otpReq.GetInt32() : 0,
                                otpToken = bene.TryGetProperty("otpToken", out var otpToken) && otpToken.ValueKind != JsonValueKind.Null ? otpToken.GetString() : null,
                                imps = bene.TryGetProperty("imps", out var imps) ? imps.GetInt32() : 0,
                                neft = bene.TryGetProperty("neft", out var neft) ? neft.GetInt32() : 0,
                                isCoolingPeriod = bene.TryGetProperty("isCoolingPeriod", out var cooling) ? cooling.GetInt32() : 0
                            });
                        }
                    }

                    return new PPIBeneListResponse
                    {
                        Status_Code = "1",
                        Message = resultMessage.GetString() ?? "Beneficiary List",
                        Data = beneficiaries
                    };
                }
                else
                {
                    return new PPIBeneListResponse
                    {
                        Status_Code = "0",
                        Message = resultMessage.GetString() ?? "Failed to get beneficiary list",
                        Data = new List<PPIBeneficiary>()
                    };
                }
            }

            // Fallback for unexpected response format
            _logger.LogWarning("Unexpected PPI Beneficiary List API response format: {Response}", responseContent);
            return new PPIBeneListResponse
            {
                Status_Code = "0",
                Message = "Unexpected response from beneficiary list service",
                Data = new List<PPIBeneficiary>()
            };
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error occurred while calling PPI Beneficiary List API");
            return new PPIBeneListResponse
            {
                Status_Code = "0",
                Message = "Network error. Please check your connection.",
                Data = new List<PPIBeneficiary>()
            };
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogError(ex, "Request timeout while calling PPI Beneficiary List API");
            return new PPIBeneListResponse
            {
                Status_Code = "0",
                Message = "Request timeout. Please try again.",
                Data = new List<PPIBeneficiary>()
            };
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Error parsing PPI Beneficiary List API response");
            return new PPIBeneListResponse
            {
                Status_Code = "0",
                Message = "Error processing response.",
                Data = new List<PPIBeneficiary>()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in PPI Beneficiary List");
            return new PPIBeneListResponse
            {
                Status_Code = "0",
                Message = "An unexpected error occurred.",
                Data = new List<PPIBeneficiary>()
            };
        }
    }

    public async Task<PPIAddBeneResponse> AddBeneficiaryAsync(PPIAddBeneRequest request)
    {
        try
        {
            // Validate API Key
            if (request.APIKey != "PPI01")
            {
                _logger.LogWarning("Invalid API Key provided for PPI Add Beneficiary");
                return new PPIAddBeneResponse
                {
                    Status_Code = "0",
                    Message = "Invalid API Key",
                    Data = ""
                };
            }

            // Generate partner transaction reference ID
            string partnertxnrefid = "TXN" + Guid.NewGuid().ToString("N").ToUpper();

            // Prepare the request payload for PPI API
            var payload = new
            {
                mobilenumber = request.SenderMobile,
                partnertxnrefid = partnertxnrefid,
                beneficiarymobilenumber = request.SenderMobile,
                beneficiaryname = request.BeneName,
                bankid = "0",
                bankaccountnumber = request.AccountNo,
                ifsccode = request.IfscCode,
                bankName = request.BankName,
                verifybeneficiary = false
            };

            var jsonPayload = JsonSerializer.Serialize(payload);
            var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

            _logger.LogInformation("Sending PPI Add Beneficiary request for Mobile: {Mobile}, Account: {Account}", 
                request.SenderMobile, request.AccountNo);

            // Make the API call
            using var httpReq = CreatePpiRequest("v2/beneficiary/add", content, request.TokeyKey);
            var response = await _httpClient.SendAsync(httpReq);
            
            var responseContent = await response.Content.ReadAsStringAsync();
            _logger.LogInformation("PPI Add Beneficiary API Response: {Response}", responseContent);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("PPI Add Beneficiary API returned error status: {StatusCode}, Response: {Response}", 
                    response.StatusCode, responseContent);
                
                return new PPIAddBeneResponse
                {
                    Status_Code = "0",
                    Message = "Failed to add beneficiary. Please try again later.",
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
                if (resultCode.GetRawText().Trim('"') == "2000" && root.TryGetProperty("result", out var result))
                {
                    string data = result.TryGetProperty("otpToken", out var otpToken) ? otpToken.GetString() ?? "" : "";

                    return new PPIAddBeneResponse
                    {
                        Status_Code = "1",
                        Message = resultMessage.GetString() ?? "Beneficiary Registration Success",
                        Data = data
                    };
                }
                else
                {
                    return new PPIAddBeneResponse
                    {
                        Status_Code = "0",
                        Message = resultMessage.GetString() ?? "Failed to add beneficiary",
                        Data = ""
                    };
                }
            }

            // Fallback for unexpected response format
            _logger.LogWarning("Unexpected PPI Add Beneficiary API response format: {Response}", responseContent);
            return new PPIAddBeneResponse
            {
                Status_Code = "0",
                Message = "Unexpected response from add beneficiary service",
                Data = ""
            };
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error occurred while calling PPI Add Beneficiary API");
            return new PPIAddBeneResponse
            {
                Status_Code = "0",
                Message = "Network error. Please check your connection.",
                Data = ""
            };
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogError(ex, "Request timeout while calling PPI Add Beneficiary API");
            return new PPIAddBeneResponse
            {
                Status_Code = "0",
                Message = "Request timeout. Please try again.",
                Data = ""
            };
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Error parsing PPI Add Beneficiary API response");
            return new PPIAddBeneResponse
            {
                Status_Code = "0",
                Message = "Error processing response.",
                Data = ""
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in PPI Add Beneficiary");
            return new PPIAddBeneResponse
            {
                Status_Code = "0",
                Message = "An unexpected error occurred.",
                Data = ""
            };
        }
    }

    public async Task<PPIResendOtpResponse> ResendOtpAsync(PPIResendOtpRequest request)
    {
        try
        {
            // Validate API Key
            if (request.APIKey != "PPI01")
            {
                _logger.LogWarning("Invalid API Key provided for PPI Resend OTP");
                return new PPIResendOtpResponse
                {
                    Status_Code = "0",
                    Message = "Invalid API Key",
                    Data = ""
                };
            }

            // Prepare the request payload for PPI API
            var payload = new
            {
                otptoken = request.otptoken
            };

            var jsonPayload = JsonSerializer.Serialize(payload);
            var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

            _logger.LogInformation("Sending PPI Resend OTP request for UserId: {UserId}", request.UserId);

            // Make the API call
            using var httpReq = CreatePpiRequest("v2/beneficiary/generateotp", content, request.tokenkey);
            var response = await _httpClient.SendAsync(httpReq);
            
            var responseContent = await response.Content.ReadAsStringAsync();
            _logger.LogInformation("PPI Resend OTP API Response: {Response}", responseContent);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("PPI Resend OTP API returned error status: {StatusCode}, Response: {Response}", 
                    response.StatusCode, responseContent);
                
                return new PPIResendOtpResponse
                {
                    Status_Code = "0",
                    Message = "Failed to resend OTP. Please try again later.",
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
                if (resultCode.GetRawText().Trim('"') == "2000")
                {
                    

                    return new PPIResendOtpResponse
                    {
                        Status_Code = "1",
                        Message = resultMessage.GetString() ?? "OTP sent successfully",
                        Data = request.otptoken
                    };
                }
                else
                {
                    return new PPIResendOtpResponse
                    {
                        Status_Code = "0",
                        Message = resultMessage.GetString() ?? "Failed to resend OTP",
                        Data = request.otptoken
                    };
                }
            }

            // Fallback for unexpected response format
            _logger.LogWarning("Unexpected PPI Resend OTP API response format: {Response}", responseContent);
            return new PPIResendOtpResponse
            {
                Status_Code = "0",
                Message = "Unexpected response from resend OTP service",
                Data = request.otptoken
            };
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error occurred while calling PPI Resend OTP API");
            return new PPIResendOtpResponse
            {
                Status_Code = "0",
                Message = "Network error. Please check your connection.",
                Data = request.otptoken
            };
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogError(ex, "Request timeout while calling PPI Resend OTP API");
            return new PPIResendOtpResponse
            {
                Status_Code = "0",
                Message = "Request timeout. Please try again.",
                Data = request.otptoken
            };
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Error parsing PPI Resend OTP API response");
            return new PPIResendOtpResponse
            {
                Status_Code = "0",
                Message = "Error processing response.",
                Data = request.otptoken
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in PPI Resend OTP");
            return new PPIResendOtpResponse
            {
                Status_Code = "0",
                Message = "An unexpected error occurred.",
                Data = request.otptoken
            };
        }
    }

    public async Task<PPIValidateOtpResponse> ValidateOtpAsync(PPIValidateOtpRequest request)
    {
        try
        {
            // Validate API Key
            if (request.APIKey != "PPI01")
            {
                _logger.LogWarning("Invalid API Key provided for PPI Validate OTP");
                return new PPIValidateOtpResponse
                {
                    Status_Code = "0",
                    Message = "Invalid API Key",
                    Data = ""
                };
            }

            // Prepare the request payload for PPI API
            var payload = new
            {
                otp = request.otp,
                otptoken = request.otptoken
            };

            var jsonPayload = JsonSerializer.Serialize(payload);
            var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

            _logger.LogInformation("Sending PPI Validate OTP request for UserId: {UserId}", request.UserId);

            // Make the API call
            using var httpReq = CreatePpiRequest("v2/beneficiary/validateotp", content, request.tokenkey);
            var response = await _httpClient.SendAsync(httpReq);
            
            var responseContent = await response.Content.ReadAsStringAsync();
            _logger.LogInformation("PPI Validate OTP API Response: {Response}", responseContent);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("PPI Validate OTP API returned error status: {StatusCode}, Response: {Response}", 
                    response.StatusCode, responseContent);
                
                return new PPIValidateOtpResponse
                {
                    Status_Code = "0",
                    Message = "Failed to validate OTP. Please try again later.",
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
                if (resultCode.GetRawText().Trim('"') == "2000")
                {
                    return new PPIValidateOtpResponse
                    {
                        Status_Code = "1",
                        Message = resultMessage.GetString() ?? "OTP validated successfully",
                        Data = ""
                    };
                }
                else
                {
                    return new PPIValidateOtpResponse
                    {
                        Status_Code = "0",
                        Message = resultMessage.GetString() ?? "Failed to validate OTP",
                        Data = ""
                    };
                }
            }

            // Fallback for unexpected response format
            _logger.LogWarning("Unexpected PPI Validate OTP API response format: {Response}", responseContent);
            return new PPIValidateOtpResponse
            {
                Status_Code = "0",
                Message = "Unexpected response from validate OTP service",
                Data = ""
            };
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error occurred while calling PPI Validate OTP API");
            return new PPIValidateOtpResponse
            {
                Status_Code = "0",
                Message = "Network error. Please check your connection.",
                Data = ""
            };
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogError(ex, "Request timeout while calling PPI Validate OTP API");
            return new PPIValidateOtpResponse
            {
                Status_Code = "0",
                Message = "Request timeout. Please try again.",
                Data = ""
            };
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Error parsing PPI Validate OTP API response");
            return new PPIValidateOtpResponse
            {
                Status_Code = "0",
                Message = "Error processing response.",
                Data = ""
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in PPI Validate OTP");
            return new PPIValidateOtpResponse
            {
                Status_Code = "0",
                Message = "An unexpected error occurred.",
                Data = ""
            };
        }
    }

    public async Task<PPIDeleteOtpResponse> DeleteGetOtpAsync(PPIDeleteOtpRequest request)
    {
        try
        {
            // Validate API Key
            if (request.APIKey != "PPI01")
            {
                _logger.LogWarning("Invalid API Key provided for PPI Delete Get OTP");
                return new PPIDeleteOtpResponse
                {
                    Status_Code = "0",
                    Message = "Invalid API Key",
                    Data = ""
                };
            }

            // Prepare the request payload for PPI API
            var payload = new
            {
                mobilenumber = request.mobilenumber,
                beneficiaryid = request.beneficiaryid
            };

            var jsonPayload = JsonSerializer.Serialize(payload);
            var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

            _logger.LogInformation("Sending PPI Delete Get OTP request for Mobile: {Mobile}, BeneficiaryId: {BeneficiaryId}", 
                request.mobilenumber, request.beneficiaryid);

            // Make the API call
            using var httpReq = CreatePpiRequest("v1/beneficiary/delete/getotp", content, request.tokenkey);
            var response = await _httpClient.SendAsync(httpReq);
            
            var responseContent = await response.Content.ReadAsStringAsync();
            _logger.LogInformation("PPI Delete Get OTP API Response: {Response}", responseContent);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("PPI Delete Get OTP API returned error status: {StatusCode}, Response: {Response}", 
                    response.StatusCode, responseContent);
                
                return new PPIDeleteOtpResponse
                {
                    Status_Code = "0",
                    Message = "Failed to get delete OTP. Please try again later.",
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
                if (resultCode.GetRawText().Trim('"') == "2000" && root.TryGetProperty("result", out var result))
                {
                    string otpToken = result.TryGetProperty("otpToken", out var oToken) ? oToken.GetString() ?? "" : "";

                    return new PPIDeleteOtpResponse
                    {
                        Status_Code = "1",
                        Message = resultMessage.GetString() ?? "OTP Sent Successfully!",
                        Data = otpToken
                    };
                }
                else
                {
                    return new PPIDeleteOtpResponse
                    {
                        Status_Code = "0",
                        Message = resultMessage.GetString() ?? "Failed to get delete OTP",
                        Data = ""
                    };
                }
            }

            // Fallback for unexpected response format
            _logger.LogWarning("Unexpected PPI Delete Get OTP API response format: {Response}", responseContent);
            return new PPIDeleteOtpResponse
            {
                Status_Code = "0",
                Message = "Unexpected response from delete get OTP service",
                Data = ""
            };
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error occurred while calling PPI Delete Get OTP API");
            return new PPIDeleteOtpResponse
            {
                Status_Code = "0",
                Message = "Network error. Please check your connection.",
                Data = ""
            };
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogError(ex, "Request timeout while calling PPI Delete Get OTP API");
            return new PPIDeleteOtpResponse
            {
                Status_Code = "0",
                Message = "Request timeout. Please try again.",
                Data = ""
            };
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Error parsing PPI Delete Get OTP API response");
            return new PPIDeleteOtpResponse
            {
                Status_Code = "0",
                Message = "Error processing response.",
                Data = ""
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in PPI Delete Get OTP");
            return new PPIDeleteOtpResponse
            {
                Status_Code = "0",
                Message = "An unexpected error occurred.",
                Data = ""
            };
        }
    }

    public async Task<PPIDeleteVerifyResponse> DeleteVerifyOtpAsync(PPIDeleteVerifyRequest request)
    {
        try
        {
            // Validate API Key
            if (request.APIKey != "PPI01")
            {
                _logger.LogWarning("Invalid API Key provided for PPI Delete Verify OTP");
                return new PPIDeleteVerifyResponse
                {
                    Status_Code = "0",
                    Message = "Invalid API Key",
                    Data = ""
                };
            }

            // Prepare the request payload for PPI API
            var payload = new
            {
                mobilenumber = request.mobilenumber,
                otpToken = request.otpToken,
                otp = request.otp
            };

            var jsonPayload = JsonSerializer.Serialize(payload);
            var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

            _logger.LogInformation("Sending PPI Delete Verify OTP request for Mobile: {Mobile}", request.mobilenumber);

            // Make the API call
            using var httpReq = CreatePpiRequest("v1/beneficiary/delete/verifyotpanddelete", content, request.tokenkey);
            var response = await _httpClient.SendAsync(httpReq);
            
            var responseContent = await response.Content.ReadAsStringAsync();
            _logger.LogInformation("PPI Delete Verify OTP API Response: {Response}", responseContent);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("PPI Delete Verify OTP API returned error status: {StatusCode}, Response: {Response}", 
                    response.StatusCode, responseContent);
                
                return new PPIDeleteVerifyResponse
                {
                    Status_Code = "0",
                    Message = "Failed to verify and delete beneficiary. Please try again later.",
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
                if (resultCode.GetRawText().Trim('"') == "2000")
                {
                    string message = resultMessage.GetString() ?? "OTP Verified.Beneficiary Deleted Successfully.";

                    return new PPIDeleteVerifyResponse
                    {
                        Status_Code = "1",
                        Message = message,
                        Data = message
                    };
                }
                else
                {
                    return new PPIDeleteVerifyResponse
                    {
                        Status_Code = "0",
                        Message = resultMessage.GetString() ?? "Failed to verify and delete beneficiary",
                        Data = ""
                    };
                }
            }

            // Fallback for unexpected response format
            _logger.LogWarning("Unexpected PPI Delete Verify OTP API response format: {Response}", responseContent);
            return new PPIDeleteVerifyResponse
            {
                Status_Code = "0",
                Message = "Unexpected response from delete verify OTP service",
                Data = ""
            };
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error occurred while calling PPI Delete Verify OTP API");
            return new PPIDeleteVerifyResponse
            {
                Status_Code = "0",
                Message = "Network error. Please check your connection.",
                Data = ""
            };
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogError(ex, "Request timeout while calling PPI Delete Verify OTP API");
            return new PPIDeleteVerifyResponse
            {
                Status_Code = "0",
                Message = "Request timeout. Please try again.",
                Data = ""
            };
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Error parsing PPI Delete Verify OTP API response");
            return new PPIDeleteVerifyResponse
            {
                Status_Code = "0",
                Message = "Error processing response.",
                Data = ""
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in PPI Delete Verify OTP");
            return new PPIDeleteVerifyResponse
            {
                Status_Code = "0",
                Message = "An unexpected error occurred.",
                Data = ""
            };
        }
    }
}

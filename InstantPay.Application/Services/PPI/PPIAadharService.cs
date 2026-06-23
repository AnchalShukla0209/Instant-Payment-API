using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using InstantPay.Application.DTOs;
using InstantPay.Application.Interfaces.PPI;
using InstantPay.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace InstantPay.Application.Services.PPI;

public class PPIAadharService : IPPIAadharService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<PPIAadharService> _logger;
    private readonly IWalletService _walletService;
    private readonly string _baseUrl;
    private readonly string _appId;
    private readonly string _authKey;
    private readonly string _secretKey;
    private const string EncryptionKey = "nh03T17a23n99sh5";

    public PPIAadharService(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<PPIAadharService> logger,
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

    private static byte[] AesEncrypt(string plainText, byte[] key, byte[] iv)
    {
        if (plainText == null || plainText.Length <= 0)
            throw new ArgumentNullException("plainText");
        if (key == null || key.Length <= 0)
            throw new ArgumentNullException("key");
        if (iv == null || iv.Length <= 0)
            throw new ArgumentNullException("iv");

        byte[] cipherBytes;

        using (var rijAlg = new RijndaelManaged())
        {
            rijAlg.Mode = CipherMode.CBC;
            rijAlg.Padding = PaddingMode.PKCS7;
            rijAlg.FeedbackSize = 128;
            rijAlg.Key = key;
            rijAlg.IV = iv;

            ICryptoTransform encryptor = rijAlg.CreateEncryptor(rijAlg.Key, rijAlg.IV);

            using (var msEncrypt = new MemoryStream())
            {
                using (var csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write))
                {
                    using (var swEncrypt = new StreamWriter(csEncrypt))
                    {
                        swEncrypt.Write(plainText);
                    }
                }
                cipherBytes = msEncrypt.ToArray();
            }
        }
        return cipherBytes;
    }

    private static string AesDecrypt(byte[] cipherBytes, byte[] key, byte[] iv)
    {
        if (cipherBytes == null || cipherBytes.Length <= 0)
            throw new ArgumentNullException("cipherBytes");
        if (key == null || key.Length <= 0)
            throw new ArgumentNullException("key");
        if (iv == null || iv.Length <= 0)
            throw new ArgumentNullException("iv");

        string plaintext = null;

        using (var rijAlg = new RijndaelManaged())
        {
            rijAlg.Mode = CipherMode.CBC;
            rijAlg.Padding = PaddingMode.PKCS7;
            rijAlg.FeedbackSize = 128;
            rijAlg.Key = key;
            rijAlg.IV = iv;

            ICryptoTransform decryptor = rijAlg.CreateDecryptor(rijAlg.Key, rijAlg.IV);

            using (var msDecrypt = new MemoryStream(cipherBytes))
            {
                using (var csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read))
                {
                    using (var srDecrypt = new StreamReader(csDecrypt))
                    {
                        plaintext = srDecrypt.ReadToEnd();
                    }
                }
            }
        }

        return plaintext;
    }

    public async Task<PPIAadharOtpResponse> GenerateAadharOtpAsync(PPIAadharOtpRequest request)
    {
        try
        {
            // Validate API Key
            if (request.APIKey != "PPI01")
            {
                _logger.LogWarning("Invalid API Key provided for PPI Aadhar OTP");
                return new PPIAadharOtpResponse
                {
                    Status_Code = "0",
                    Message = "Invalid API Key",
                    Data = ""
                };
            }

            // Prepare the request payload for PPI API
            var payload = new
            {
                aadharno = request.AadharNo,
                consentid = request.ConsentId,
                walletaccreatorcode = "C3",
                walletaccreatorname = request.RTName,
                walletacapplicationnumber = request.ApplicationNumber,
                walletaccreatorpincode = request.pincode
            };

            var jsonPayload = JsonSerializer.Serialize(payload);
            var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

            _logger.LogInformation("Sending PPI Aadhar OTP request for AadharNo: {AadharNo}", request.AadharNo);

            // Add headers
            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("AppID", _appId);
            _httpClient.DefaultRequestHeaders.Add("AuthKey", _authKey);
            _httpClient.DefaultRequestHeaders.Add("SecretKey", _secretKey);
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {request.TokeyKey}");

            // Make the API call
            var response = await _httpClient.PostAsync($"{_baseUrl}v1/kyc/aadhaargenerateotp", content);
            
            var responseContent = await response.Content.ReadAsStringAsync();
            _logger.LogInformation("PPI Aadhar OTP API Response: {Response}", responseContent);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("PPI Aadhar OTP API returned error status: {StatusCode}, Response: {Response}", 
                    response.StatusCode, responseContent);
                
                return new PPIAadharOtpResponse
                {
                    Status_Code = "0",
                    Message = "Failed to send Aadhar OTP. Please try again later.",
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

                    // Debit 10 Rs from user wallet on success
                    if (int.TryParse(request.UserId, out int userId))
                    {
                        try
                        {
                            await _walletService.DebitAsync(
                                userId,
                                "PPI KYC",
                                10,
                                10,
                                0,
                                0,
                                "PPI KYC",
                                "Amount Debit For PPI KYC",
                                request.RTName);
                            _logger.LogInformation("Debited 10 Rs from user {UserId} for PPI Aadhar OTP", userId);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Failed to debit wallet for user {UserId}", userId);
                        }
                    }

                    return new PPIAadharOtpResponse
                    {
                        Status_Code = "1",
                        Message = resultMessage.GetString() ?? "OTP Sent Successfully",
                        Data = otpToken
                    };
                }
                else
                {
                    return new PPIAadharOtpResponse
                    {
                        Status_Code = "0",
                        Message = resultMessage.GetString() ?? "Failed to send Aadhar OTP",
                        Data = ""
                    };
                }
            }

            // Fallback for unexpected response format
            _logger.LogWarning("Unexpected PPI Aadhar OTP API response format: {Response}", responseContent);
            return new PPIAadharOtpResponse
            {
                Status_Code = "0",
                Message = "Unexpected response from Aadhar OTP service",
                Data = ""
            };
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error occurred while calling PPI Aadhar OTP API");
            return new PPIAadharOtpResponse
            {
                Status_Code = "0",
                Message = "Network error. Please check your connection.",
                Data = ""
            };
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogError(ex, "Request timeout while calling PPI Aadhar OTP API");
            return new PPIAadharOtpResponse
            {
                Status_Code = "0",
                Message = "Request timeout. Please try again.",
                Data = ""
            };
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Error parsing PPI Aadhar OTP API response");
            return new PPIAadharOtpResponse
            {
                Status_Code = "0",
                Message = "Error processing response.",
                Data = ""
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in PPI Aadhar OTP");
            return new PPIAadharOtpResponse
            {
                Status_Code = "0",
                Message = "An unexpected error occurred.",
                Data = ""
            };
        }
    }

    public async Task<PPIValidateAadharOtpResponse> ValidateAadharOtpAsync(PPIValidateAadharOtpRequest request)
    {
        try
        {
            // Validate API Key
            if (request.APIKey != "PPI01")
            {
                _logger.LogWarning("Invalid API Key provided for PPI Validate Aadhar OTP");
                return new PPIValidateAadharOtpResponse
                {
                    Status_Code = "0",
                    Message = "Invalid API Key",
                    Data = ""
                };
            }

            // Prepare the request payload for PPI API
            var payload = new
            {
                walletAcApplicationNumber = request.ApplicationNumber,
                otpToken = request.AadharToken,
                otp = request.OTP
            };

            var jsonPayload = JsonSerializer.Serialize(payload);
            var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

            _logger.LogInformation("Sending PPI Validate Aadhar OTP request for ApplicationNumber: {ApplicationNumber}, SenderMobile: {SenderMobile}", 
                request.ApplicationNumber, request.SenderMobile);

            // Add headers
            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("AppID", _appId);
            _httpClient.DefaultRequestHeaders.Add("AuthKey", _authKey);
            _httpClient.DefaultRequestHeaders.Add("SecretKey", _secretKey);
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {request.TokeyKey}");

            // Make the API call
            var response = await _httpClient.PostAsync($"{_baseUrl}v1/kyc/aadhaarvalidateotp", content);
            
            var responseContent = await response.Content.ReadAsStringAsync();
            _logger.LogInformation("PPI Validate Aadhar OTP API Response: {Response}", responseContent);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("PPI Validate Aadhar OTP API returned error status: {StatusCode}, Response: {Response}", 
                    response.StatusCode, responseContent);
                
                return new PPIValidateAadharOtpResponse
                {
                    Status_Code = "0",
                    Message = "Failed to validate Aadhar OTP. Please try again later.",
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
                if (resultCode.GetString() == "2000")
                {
                    string message = resultMessage.GetString() ?? "Sender Created";

                    // Debit 10 Rs from user wallet on success
                    if (int.TryParse(request.UserId, out int userId))
                    {
                        try
                        {
                            await _walletService.DebitAsync(
                                userId,
                                "PPI KYC",
                                10,
                                10,
                                0,
                                0,
                                "PPI KYC",
                                "Amount Debit For PPI KYC",
                                request.SenderMobile);
                            _logger.LogInformation("Debited 10 Rs from user {UserId} for PPI Validate Aadhar OTP", userId);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Failed to debit wallet for user {UserId}", userId);
                        }
                    }

                    return new PPIValidateAadharOtpResponse
                    {
                        Status_Code = "1",
                        Message = message,
                        Data = ""
                    };
                }
                else
                {
                    string message = resultMessage.GetString() ?? "Failed to validate Aadhar OTP";

                    return new PPIValidateAadharOtpResponse
                    {
                        Status_Code = "0",
                        Message = message,
                        Data = message
                    };
                }
            }

            // Fallback for unexpected response format
            _logger.LogWarning("Unexpected PPI Validate Aadhar OTP API response format: {Response}", responseContent);
            return new PPIValidateAadharOtpResponse
            {
                Status_Code = "0",
                Message = "Unexpected response from validate Aadhar OTP service",
                Data = ""
            };
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error occurred while calling PPI Validate Aadhar OTP API");
            return new PPIValidateAadharOtpResponse
            {
                Status_Code = "0",
                Message = "Network error. Please check your connection.",
                Data = ""
            };
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogError(ex, "Request timeout while calling PPI Validate Aadhar OTP API");
            return new PPIValidateAadharOtpResponse
            {
                Status_Code = "0",
                Message = "Request timeout. Please try again.",
                Data = ""
            };
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Error parsing PPI Validate Aadhar OTP API response");
            return new PPIValidateAadharOtpResponse
            {
                Status_Code = "0",
                Message = "Error processing response.",
                Data = ""
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in PPI Validate Aadhar OTP");
            return new PPIValidateAadharOtpResponse
            {
                Status_Code = "0",
                Message = "An unexpected error occurred.",
                Data = ""
            };
        }
    }

    public async Task<PPIAadharBiometricResponse> AadharBiometricAsync(PPIAadharBiometricRequest request)
    {
        try
        {
            // Validate API Key
            if (request.APIKey != "PPI01")
            {
                _logger.LogWarning("Invalid API Key provided for PPI Aadhar Biometric");
                return new PPIAadharBiometricResponse
                {
                    Status_Code = "0",
                    Message = "Invalid API Key",
                    Data = ""
                };
            }

            // Generate client transaction ID
            string clienttxnid = "TXN" + DateTime.Now.Ticks.ToString();

            // Prepare the request payload for PPI API
            var payload = new
            {
                walletaccreatorcode = "C3",
                walletaccreatorpincode = request.pincode,
                walletaccreatorname = request.RTName,
                walletacapplicationnumber = request.ApplicationNumber,
                mobilenumber = request.SenderMobile,
                aadhaarnumber = request.AadharNo,
                latitude = request.latitude,
                longitude = request.longitude,
                consent = request.ConsentId,
                consenttaken = "True",
                biometricdata = request.biometricdata,
                clienttxnid = clienttxnid
            };

            var jsonPayload = JsonSerializer.Serialize(payload);
            
            // Encrypt the payload
            byte[] keyBytes = Encoding.UTF8.GetBytes(EncryptionKey);
            byte[] ivBytes = Encoding.UTF8.GetBytes(EncryptionKey);
            byte[] encryptedBytes = AesEncrypt(jsonPayload, keyBytes, ivBytes);
            string encryptedText = Convert.ToBase64String(encryptedBytes);

            var encryptedPayload = new
            {
                requestBody = encryptedText
            };

            var finalJsonPayload = JsonSerializer.Serialize(encryptedPayload);
            var content = new StringContent(finalJsonPayload, Encoding.UTF8, "application/json");

            _logger.LogInformation("Sending PPI Aadhar Biometric request for ApplicationNumber: {ApplicationNumber}, SenderMobile: {SenderMobile}", 
                request.ApplicationNumber, request.SenderMobile);

            // Add headers
            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("AppID", _appId);
            _httpClient.DefaultRequestHeaders.Add("AuthKey", _authKey);
            _httpClient.DefaultRequestHeaders.Add("SecretKey", _secretKey);
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {request.TokeyKey}");

            // Make the API call
            var response = await _httpClient.PostAsync($"{_baseUrl}v2/kyc/dobiometricekyc", content);
            
            var responseContent = await response.Content.ReadAsStringAsync();
            _logger.LogInformation("PPI Aadhar Biometric API Response: {Response}", responseContent);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("PPI Aadhar Biometric API returned error status: {StatusCode}, Response: {Response}", 
                    response.StatusCode, responseContent);
                
                return new PPIAadharBiometricResponse
                {
                    Status_Code = "0",
                    Message = "Failed to process Aadhar biometric. Please try again later.",
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
                if (resultCode.GetString() == "2000")
                {
                    string message = resultMessage.GetString() ?? "Sender Created";

                    // Debit 10 Rs from user wallet on success
                    if (int.TryParse(request.UserId, out int userId))
                    {
                        try
                        {
                            await _walletService.DebitAsync(
                                userId,
                                "PPI KYC",
                                10,
                                10,
                                0,
                                0,
                                "PPI KYC",
                                "Amount Debit For PPI KYC",
                                request.SenderMobile);
                            _logger.LogInformation("Debited 10 Rs from user {UserId} for PPI Aadhar Biometric", userId);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Failed to debit wallet for user {UserId}", userId);
                        }
                    }

                    return new PPIAadharBiometricResponse
                    {
                        Status_Code = "1",
                        Message = message,
                        Data = message
                    };
                }
                else
                {
                    string message = resultMessage.GetString() ?? "Failed to process Aadhar biometric";

                    return new PPIAadharBiometricResponse
                    {
                        Status_Code = "0",
                        Message = message,
                        Data = message
                    };
                }
            }

            // Fallback for unexpected response format
            _logger.LogWarning("Unexpected PPI Aadhar Biometric API response format: {Response}", responseContent);
            return new PPIAadharBiometricResponse
            {
                Status_Code = "0",
                Message = "Unexpected response from Aadhar biometric service",
                Data = ""
            };
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error occurred while calling PPI Aadhar Biometric API");
            return new PPIAadharBiometricResponse
            {
                Status_Code = "0",
                Message = "Network error. Please check your connection.",
                Data = ""
            };
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogError(ex, "Request timeout while calling PPI Aadhar Biometric API");
            return new PPIAadharBiometricResponse
            {
                Status_Code = "0",
                Message = "Request timeout. Please try again.",
                Data = ""
            };
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Error parsing PPI Aadhar Biometric API response");
            return new PPIAadharBiometricResponse
            {
                Status_Code = "0",
                Message = "Error processing response.",
                Data = ""
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in PPI Aadhar Biometric");
            return new PPIAadharBiometricResponse
            {
                Status_Code = "0",
                Message = "An unexpected error occurred.",
                Data = ""
            };
        }
    }

    public async Task<PPIPanResponse> ValidatePanAsync(PPIPanRequest request)
    {
        try
        {
            // Validate API Key
            if (request.APIKey != "PPI01")
            {
                _logger.LogWarning("Invalid API Key provided for PPI Pan Validation");
                return new PPIPanResponse
                {
                    Status_Code = "0",
                    Message = "Invalid API Key",
                    Data = ""
                };
            }

            // Generate partner transaction reference ID
            string partnertxnrefid = "TXN" + DateTime.Now.Ticks.ToString();

            // Prepare the request payload for PPI API
            var payload = new
            {
                walletAcCreatorCode = "C3",
                walletAcCreatorPinCode = request.pincode,
                walletAcCreatorName = request.RTName,
                walletAcApplicationNumber = request.ApplicationNumber,
                pancardNumber = request.PancardNo,
                partnertxnrefid = partnertxnrefid
            };

            var jsonPayload = JsonSerializer.Serialize(payload);
            var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

            _logger.LogInformation("Sending PPI Pan validation request for ApplicationNumber: {ApplicationNumber}, PancardNo: {PancardNo}", 
                request.ApplicationNumber, request.PancardNo);

            // Add headers
            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("AppID", _appId);
            _httpClient.DefaultRequestHeaders.Add("AuthKey", _authKey);
            _httpClient.DefaultRequestHeaders.Add("SecretKey", _secretKey);
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {request.TokeyKey}");

            // Make the API call
            var response = await _httpClient.PostAsync($"{_baseUrl}v1/kyc/pancard", content);
            
            var responseContent = await response.Content.ReadAsStringAsync();
            _logger.LogInformation("PPI Pan validation API Response: {Response}", responseContent);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("PPI Pan validation API returned error status: {StatusCode}, Response: {Response}", 
                    response.StatusCode, responseContent);
                
                return new PPIPanResponse
                {
                    Status_Code = "0",
                    Message = "Failed to validate PAN card. Please try again later.",
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
                if (resultCode.GetString() == "2000")
                {
                    string message = resultMessage.GetString() ?? "PAN card validated successfully";

                    // Debit 10 Rs from user wallet on success
                    if (int.TryParse(request.UserId, out int userId))
                    {
                        try
                        {
                            await _walletService.DebitAsync(
                                userId,
                                "PPI KYC",
                                10,
                                10,
                                0,
                                0,
                                "PPI KYC",
                                "Amount Debit For PPI KYC",
                                request.RTName);
                            _logger.LogInformation("Debited 10 Rs from user {UserId} for PPI Pan Validation", userId);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Failed to debit wallet for user {UserId}", userId);
                        }
                    }

                    return new PPIPanResponse
                    {
                        Status_Code = "1",
                        Message = message,
                        Data = ""
                    };
                }
                else
                {
                    string message = resultMessage.GetString() ?? "Failed to validate PAN card";

                    return new PPIPanResponse
                    {
                        Status_Code = "0",
                        Message = message,
                        Data = ""
                    };
                }
            }

            // Fallback for unexpected response format
            _logger.LogWarning("Unexpected PPI Pan validation API response format: {Response}", responseContent);
            return new PPIPanResponse
            {
                Status_Code = "0",
                Message = "Unexpected response from PAN validation service",
                Data = ""
            };
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error occurred while calling PPI Pan validation API");
            return new PPIPanResponse
            {
                Status_Code = "0",
                Message = "Network error. Please check your connection.",
                Data = ""
            };
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogError(ex, "Request timeout while calling PPI Pan validation API");
            return new PPIPanResponse
            {
                Status_Code = "0",
                Message = "Request timeout. Please try again.",
                Data = ""
            };
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Error parsing PPI Pan validation API response");
            return new PPIPanResponse
            {
                Status_Code = "0",
                Message = "Error processing response.",
                Data = ""
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in PPI Pan validation");
            return new PPIPanResponse
            {
                Status_Code = "0",
                Message = "An unexpected error occurred.",
                Data = ""
            };
        }
    }
}

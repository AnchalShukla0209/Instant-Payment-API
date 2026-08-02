using InstantPay.Application.DTOs;
using InstantPay.Application.Interfaces;
using InstantPay.Application.Interfaces.PPI;
using InstantPay.Infrastructure.Sql.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using static Microsoft.AspNetCore.Hosting.Internal.HostingApplication;

namespace InstantPay.Application.Services.PPI;

public class PPIAadharService : IPPIAadharService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<PPIAadharService> _logger;
    private readonly IWalletService _walletService;
    private readonly string _baseUrl;
    private readonly string _appId;
    private readonly string _authKey;
    private readonly string _secretKey;
    private const string EncryptionKey = "nh03T17a23n99sh5";
    private readonly AppDbContext _context;

    public PPIAadharService(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<PPIAadharService> logger,
        IWalletService walletService,
        AppDbContext context)
    {
        _httpClient = httpClientFactory.CreateClient();
        _logger = logger;
        _walletService = walletService;

        // Load PPI configuration from appsettings
        var ppiConfig = configuration.GetSection("PPI");
        _baseUrl = ppiConfig["BaseUrl"] ?? "https://api.digikhata.in/p2a/";
        _appId = ppiConfig["AppId"] ?? "INSTANTPAYMENT";
        _authKey = ppiConfig["AuthKey"] ?? string.Empty;
        _secretKey = ppiConfig["SecretKey"] ?? string.Empty;

        _httpClient.Timeout = TimeSpan.FromSeconds(30);
        _context = context;
    }

    private static byte[] AesEncrypt(string plainText, byte[] key, byte[] iv)
    {
        if (string.IsNullOrEmpty(plainText)) throw new ArgumentNullException(nameof(plainText));
        if (key == null || key.Length == 0)  throw new ArgumentNullException(nameof(key));
        if (iv  == null || iv.Length  == 0)  throw new ArgumentNullException(nameof(iv));

        using var aes = Aes.Create();
        aes.Mode    = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        aes.Key = key;
        aes.IV  = iv;

        using var ms = new MemoryStream();
        using (var cs = new CryptoStream(ms, aes.CreateEncryptor(), CryptoStreamMode.Write))
        using (var sw = new StreamWriter(cs))
            sw.Write(plainText);
        return ms.ToArray();
    }

    private static string AesDecrypt(byte[] cipherBytes, byte[] key, byte[] iv)
    {
        if (cipherBytes == null || cipherBytes.Length == 0) throw new ArgumentNullException(nameof(cipherBytes));
        if (key == null || key.Length == 0)                 throw new ArgumentNullException(nameof(key));
        if (iv  == null || iv.Length  == 0)                 throw new ArgumentNullException(nameof(iv));

        using var aes = Aes.Create();
        aes.Mode    = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        aes.Key = key;
        aes.IV  = iv;

        using var ms = new MemoryStream(cipherBytes);
        using var cs = new CryptoStream(ms, aes.CreateDecryptor(), CryptoStreamMode.Read);
        using var sr = new StreamReader(cs);
        return sr.ReadToEnd();
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

            // Make the API call
            using var httpReq = CreatePpiRequest("v1/kyc/aadhaargenerateotp", content, request.TokeyKey);
            var response = await _httpClient.SendAsync(httpReq);
            
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
                if (resultCode.GetRawText().Trim('"') == "2000" && root.TryGetProperty("result", out var result))
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

            // Make the API call
            using var httpReq = CreatePpiRequest("v1/kyc/aadhaarvalidateotp", content, request.TokeyKey);
            var response = await _httpClient.SendAsync(httpReq);
            
            var responseContent = await response.Content.ReadAsStringAsync();
            _logger.LogInformation("PPI Validate Aadhar OTP API Response: {Response}", responseContent);

            if (!response.IsSuccessStatusCode)
            {
                var log = new Apilog
                {
                    Apiname = "PPI-AdharOTPVerify",
                    Reqdatae = DateTime.Now,
                    Request = jsonPayload,
                    Response = responseContent + "||" + response.StatusCode
                };
                _context.Apilogs.Add(log);
                await _context.SaveChangesAsync();
                
                return new PPIValidateAadharOtpResponse
                {
                    Status_Code = "0",
                    Message = "Failed to validate Aadhar OTP. Please try again later.",
                    Data = ""
                };
            }

            var logs = new Apilog
            {
                Apiname = "PPI-AdharOTPVerify",
                Reqdatae = DateTime.Now,
                Request = jsonPayload,
                Response = responseContent + "||" + response.StatusCode
            };
            _context.Apilogs.Add(logs);
            await _context.SaveChangesAsync();

            // Parse the response
            using var jsonDoc = JsonDocument.Parse(responseContent);
            var root = jsonDoc.RootElement;

            // Check if the response has the expected structure
            if (root.TryGetProperty("resultCode", out var resultCode) && 
                root.TryGetProperty("resultMessage", out var resultMessage))
            {
                if (resultCode.GetRawText().Trim('"') == "2000")
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
            string clienttxnid = "TXN" + Guid.NewGuid().ToString("N").ToUpper();

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

            // Make the API call
            using var httpReq = CreatePpiRequest("v2/kyc/dobiometricekyc", content, request.TokeyKey);
            var response = await _httpClient.SendAsync(httpReq);
            
            var responseContent = await response.Content.ReadAsStringAsync();
            _logger.LogInformation("PPI Aadhar Biometric API Response: {Response}", responseContent);

            if (!response.IsSuccessStatusCode)
            {
                var log = new Apilog
                {
                    Apiname = "PPI-biometricekyc",
                    Reqdatae = DateTime.Now,
                    Request = jsonPayload,
                    Response = responseContent + "||" + response.StatusCode
                };
                _context.Apilogs.Add(log);
                await _context.SaveChangesAsync();
                
                return new PPIAadharBiometricResponse
                {
                    Status_Code = "0",
                    Message = "Failed to process Aadhar biometric. Please try again later.",
                    Data = ""
                };
            }

            var logs = new Apilog
            {
                Apiname = "PPI-biometricekyc",
                Reqdatae = DateTime.Now,
                Request = jsonPayload,
                Response = responseContent + "||" + response.StatusCode
            };
            _context.Apilogs.Add(logs);
            await _context.SaveChangesAsync();

            // Parse the response
            using var jsonDoc = JsonDocument.Parse(responseContent);
            var root = jsonDoc.RootElement;

            // Check if the response has the expected structure
            if (root.TryGetProperty("resultCode", out var resultCode) && 
                root.TryGetProperty("resultMessage", out var resultMessage))
            {
                if (resultCode.GetRawText().Trim('"') == "2000")
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
            string partnertxnrefid = "TXN" + Guid.NewGuid().ToString("N").ToUpper();

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

            // Make the API call
            using var httpReq = CreatePpiRequest("v1/kyc/pancard", content, request.TokeyKey);
            var response = await _httpClient.SendAsync(httpReq);
            
            var responseContent = await response.Content.ReadAsStringAsync();
            _logger.LogInformation("PPI Pan validation API Response: {Response}", responseContent);

            if (!response.IsSuccessStatusCode)
            {
                var logs = new Apilog
                {
                    Apiname = "PPI-panvalidate",
                    Reqdatae = DateTime.Now,
                    Request = jsonPayload,
                    Response = responseContent + "||" + response.StatusCode
                };
                _context.Apilogs.Add(logs);

                return new PPIPanResponse
                {
                    Status_Code = "0",
                    Message = "Failed to validate PAN card. Please try again later.",
                    Data = ""
                };
            }

            var log = new Apilog
            {
                Apiname = "PPI-panvalidate",
                Reqdatae = DateTime.Now,
                Request = jsonPayload,
                Response = responseContent + "||" + response.StatusCode
            };
            _context.Apilogs.Add(log);

            // Parse the response
            using var jsonDoc = JsonDocument.Parse(responseContent);
            var root = jsonDoc.RootElement;

            // Check if the response has the expected structure
            if (root.TryGetProperty("resultCode", out var resultCode) && 
                root.TryGetProperty("resultMessage", out var resultMessage))
            {
                if (resultCode.GetRawText().Trim('"') == "2000")
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
                                "PPI PAN Validation",
                                "Amount Debit For PPI PAN Validate",
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

    public async Task<PPICreateWalletResponse> CreateWalletAsync(PPICreateWalletRequest request)
    {
        try
        {
            // Validate API Key
            if (request.APIKey != "PPI01")
            {
                _logger.LogWarning("Invalid API Key provided for PPI Create Wallet");
                return new PPICreateWalletResponse
                {
                    ResultCode = "0",
                    ResultStatus = "Failure",
                    ResultMessage = "Invalid API Key",
                    Result = new PPICreateWalletResult()
                };
            }

            // Prepare the request payload for PPI API
            var payload = new
            {
                walletaccreatorcode = request.WalletAcCreatorCode,
                walletaccreatorname = request.WalletAcCreatorName,
                walletaccreatorpincode = request.WalletAcCreatorPinCode,
                mobilenumber = request.MobileNumber,
                walletAcApplicationNumber = request.WalletAcApplicationNumber,
                pancardnumber = request.PancardNumber,
                partnertxnrefId = request.PartnerTxnRefId
            };

            var jsonPayload = JsonSerializer.Serialize(payload);
            var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

            _logger.LogInformation("Sending PPI Create Wallet request for Mobile: {Mobile}, ApplicationNumber: {ApplicationNumber}",
                request.MobileNumber, request.WalletAcApplicationNumber);

            // Make the API call
            using var httpReq = CreatePpiRequest("v2/kyc/createwallet", content, request.TokeyKey);
            var response = await _httpClient.SendAsync(httpReq);

            var responseContent = await response.Content.ReadAsStringAsync();
            _logger.LogInformation("PPI Create Wallet API Response: {Response}", responseContent);

            if (!response.IsSuccessStatusCode)
            {
                var logs = new Apilog
                {
                    Apiname = "PPI-CreateWallet",
                    Reqdatae = DateTime.Now,
                    Request = jsonPayload,
                    Response = responseContent + "||" + response.StatusCode
                };
                _context.Apilogs.Add(logs);

                return new PPICreateWalletResponse
                {
                    ResultCode = "0",
                    ResultStatus = "Failure",
                    ResultMessage = "Failed to create wallet. Please try again later.",
                    Result = new PPICreateWalletResult()
                };
            }

            var log = new Apilog
            {
                Apiname = "PPI-CreateWallet",
                Reqdatae = DateTime.Now,
                Request = jsonPayload,
                Response = responseContent + "||" + response.StatusCode
            };
            _context.Apilogs.Add(log);

            // Parse the response
            using var jsonDoc = JsonDocument.Parse(responseContent);
            var root = jsonDoc.RootElement;

            var result = new PPICreateWalletResponse
            {
                ResultCode = root.TryGetProperty("resultCode", out var resultCode) ? resultCode.GetRawText().Trim('"') : "0",
                ResultStatus = root.TryGetProperty("resultStatus", out var resultStatus) ? resultStatus.GetString() ?? "" : "",
                ResultMessage = root.TryGetProperty("resultMessage", out var resultMessage) ? resultMessage.GetString() ?? "" : "",
                Result = new PPICreateWalletResult()
            };

            if (root.TryGetProperty("result", out var resultObj) && resultObj.ValueKind == JsonValueKind.Object)
            {
                result.Result = new PPICreateWalletResult
                {
                    PancardVerified = resultObj.TryGetProperty("pancardVerified", out var pancardVerified) && pancardVerified.ValueKind == JsonValueKind.True,
                    PancardPhotoRequired = resultObj.TryGetProperty("pancardPhotoRequired", out var pancardPhotoRequired) && pancardPhotoRequired.ValueKind == JsonValueKind.True,
                    WalletCreated = resultObj.TryGetProperty("walletCreated", out var walletCreated) && walletCreated.ValueKind == JsonValueKind.True,
                    WalletHolderName = resultObj.TryGetProperty("walletHolderName", out var walletHolderName) ? walletHolderName.GetString() ?? "" : "",
                    KycType = resultObj.TryGetProperty("kycType", out var kycType) ? kycType.GetString() ?? "" : "",
                    AccountStatus = resultObj.TryGetProperty("accountStatus", out var accountStatus) ? accountStatus.GetString() ?? "" : "",
                    CashTopUpLimitAvailable = resultObj.TryGetProperty("cashTopUpLimitAvailable", out var cashTopUpLimitAvailable) ? cashTopUpLimitAvailable.GetRawText() : "",
                    CashTopUpLimitConsumed = resultObj.TryGetProperty("cashTopUpLimitConsumed", out var cashTopUpLimitConsumed) ? cashTopUpLimitConsumed.GetRawText() : ""
                };
            }

            return result;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error occurred while calling PPI Create Wallet API");
            return new PPICreateWalletResponse
            {
                ResultCode = "0",
                ResultStatus = "Failure",
                ResultMessage = "Network error. Please check your connection.",
                Result = new PPICreateWalletResult()
            };
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogError(ex, "Request timeout while calling PPI Create Wallet API");
            return new PPICreateWalletResponse
            {
                ResultCode = "0",
                ResultStatus = "Failure",
                ResultMessage = "Request timeout. Please try again.",
                Result = new PPICreateWalletResult()
            };
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Error parsing PPI Create Wallet API response");
            return new PPICreateWalletResponse
            {
                ResultCode = "0",
                ResultStatus = "Failure",
                ResultMessage = "Error processing response.",
                Result = new PPICreateWalletResult()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in PPI Create Wallet");
            return new PPICreateWalletResponse
            {
                ResultCode = "0",
                ResultStatus = "Failure",
                ResultMessage = "An unexpected error occurred.",
                Result = new PPICreateWalletResult()
            };
        }
    }
}

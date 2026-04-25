using InstantPay.Application.Interfaces;
using InstantPay.Infrastructure.Sql.Entities;
using InstantPay.SharedKernel.RequestPayload.WhatsApp;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace InstantPay.Application.Services
{
    public class WhatsAppService : IWhatsAppService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _config;
        private readonly AppDbContext _context;
        private readonly ILogger<WhatsAppService> _logger;

        public WhatsAppService(
            HttpClient httpClient, 
            IConfiguration config, 
            AppDbContext context,
            ILogger<WhatsAppService> logger)
        {
            _httpClient = httpClient;
            _config = config;
            _context = context;
            _logger = logger;
        }

        public async Task<WhatsAppBroadcastResult> SendBroadcastMessageAsync(WhatsAppBroadcastRequest request)
        {
            var result = new WhatsAppBroadcastResult
            {
                SentAt = DateTime.UtcNow
            };

            IWebDriver driver = null;
            string tempProfilePath = null;

            try
            {
                // Validate request
                if (string.IsNullOrWhiteSpace(request.Message))
                {
                    result.Success = false;
                    result.Message = "Message cannot be empty";
                    return result;
                }

                if (request.Message.Length > 1000)
                {
                    result.Success = false;
                    result.Message = "Message cannot exceed 1000 characters";
                    return result;
                }

                // Fetch users from database
                var usersQuery = _context.TblUsers.AsQueryable();

                // Filter by active status if requested
                if (request.SendToActiveUsersOnly == true)
                {
                    usersQuery = usersQuery.Where(u => u.Status == "Active" || u.Status == "active");
                }

                var users = await usersQuery
                    .Where(u => !string.IsNullOrWhiteSpace(u.Phone))
                    .Select(u => new { u.Id, u.Phone, u.Name })
                    .ToListAsync();

                result.TotalUsers = users.Count;

                if (result.TotalUsers == 0)
                {
                    result.Success = false;
                    result.Message = "No users found to send message";
                    return result;
                }

                // Format phone numbers (remove spaces, dashes, ensure +91 prefix)
                var formattedPhoneNumbers = users
                    .Select(u => FormatPhoneNumber(u.Phone))
                    .Where(p => !string.IsNullOrWhiteSpace(p))
                    .Distinct()
                    .ToList();

                _logger.LogWarning("Starting WhatsApp Web Automation broadcast to {Count} users. WARNING: This method may result in account ban by WhatsApp.", formattedPhoneNumbers.Count);

                // Initialize Chrome WebDriver with isolated profile
                var chromeOptions = new ChromeOptions();
                var useHeadless = _config.GetValue<bool>("WhatsApp:HeadlessMode", true);
                
                _logger.LogInformation("Initializing Chrome WebDriver - Headless: {Headless}", useHeadless);
                
                if (useHeadless)
                {
                    chromeOptions.AddArgument("--headless=new");
                }
                chromeOptions.AddArgument("--no-sandbox");
                chromeOptions.AddArgument("--disable-dev-shm-usage");
                chromeOptions.AddArgument("--disable-blink-features=AutomationControlled");
                chromeOptions.AddExcludedArgument("enable-automation");
                chromeOptions.AddArgument("--disable-extensions");
                chromeOptions.AddArgument("--disable-gpu");
                
                // Additional arguments for server environments
                chromeOptions.AddArgument("--disable-software-rasterizer");
                chromeOptions.AddArgument("--disable-dev-shm-usage");
                chromeOptions.AddArgument("--remote-debugging-port=0"); // Use random port to avoid conflicts
                
                // Use isolated profile directory (won't conflict with your Chrome)
                tempProfilePath = Path.Combine(Path.GetTempPath(), $"WhatsAppAutomation_{Guid.NewGuid():N}");
                
                // Ensure directory exists and is clean
                if (Directory.Exists(tempProfilePath))
                {
                    try
                    {
                        Directory.Delete(tempProfilePath, recursive: true);
                        _logger.LogInformation("Cleaned up existing profile directory");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Could not clean up existing profile directory, using new path");
                        tempProfilePath = Path.Combine(Path.GetTempPath(), $"WhatsAppAutomation_{Guid.NewGuid():N}_{DateTime.Now:yyyyMMddHHmmss}");
                    }
                }
                
                Directory.CreateDirectory(tempProfilePath);
                chromeOptions.AddArgument($"--user-data-dir={tempProfilePath}");

                try
                {
                    var chromeDriverService = ChromeDriverService.CreateDefaultService();
                    chromeDriverService.SuppressInitialDiagnosticInformation = true;
                    chromeDriverService.HideCommandPromptWindow = true;
                    
                    // Allow custom Chrome binary location from configuration
                    var chromeBinaryPath = _config.GetValue<string>("WhatsApp:ChromeBinaryPath");
                    if (!string.IsNullOrWhiteSpace(chromeBinaryPath))
                    {
                        chromeOptions.BinaryLocation = chromeBinaryPath;
                        _logger.LogInformation("Using custom Chrome binary: {Path}", chromeBinaryPath);
                    }
                    
                    _logger.LogInformation("Chrome profile path: {Path}", tempProfilePath);
                    
                    driver = new ChromeDriver(chromeDriverService, chromeOptions);
                    _logger.LogInformation("Chrome started successfully with isolated profile");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to create ChromeDriver. Details: {Message}", ex.Message);
                    _logger.LogError("Chrome binary check - Please ensure Chrome is installed on the server");
                    _logger.LogError("Temp directory: {TempPath}", Path.GetTempPath());
                    
                    result.Success = false;
                    result.Message = $"Failed to initialize Chrome: {ex.Message}. Ensure Chrome is installed on the server.";
                    return result;
                }

                // Navigate to WhatsApp Web
                _logger.LogInformation("Navigating to WhatsApp Web...");
                driver.Navigate().GoToUrl("https://web.whatsapp.com");

                // Wait for page to load and check login status
                _logger.LogInformation("Checking login status...");
                await Task.Delay(5000);
                
                bool qrCodeVisible = false;
                
                // Check for QR code - if visible, not logged in
                try
                {
                    var qrCode = driver.FindElement(By.XPath("//canvas[@aria-label='Scan this QR code to link a device!']"));
                    if (qrCode.Displayed)
                    {
                        qrCodeVisible = true;
                        _logger.LogInformation("QR code visible, waiting for scan (up to 60 seconds)...");
                        
                        // Wait for QR scan
                        for (int i = 0; i < 60; i++)
                        {
                            await Task.Delay(1000);
                            try
                            {
                                qrCode = driver.FindElement(By.XPath("//canvas[@aria-label='Scan this QR code to link a device!']"));
                                if (!qrCode.Displayed)
                                {
                                    _logger.LogInformation("QR code scanned, waiting for login to complete...");
                                    await Task.Delay(3000);
                                    qrCodeVisible = false;
                                    break;
                                }
                            }
                            catch
                            {
                                _logger.LogInformation("QR code no longer visible, waiting for login...");
                                await Task.Delay(3000);
                                qrCodeVisible = false;
                                break;
                            }
                        }
                    }
                }
                catch { }

                // If QR code is not visible, assume logged in and proceed
                if (!qrCodeVisible)
                {
                    _logger.LogInformation("No QR code visible - assuming logged in, proceeding with broadcast...");
                }
                else
                {
                    _logger.LogError("QR code scan timeout");
                    result.Success = false;
                    result.Message = "Please scan QR code to login to WhatsApp Web (timeout after 60 seconds)";
                    return result;
                }

                // Send messages to each user
                foreach (var phoneNumber in formattedPhoneNumbers)
                {
                    try
                    {
                        await SendMessageViaWhatsAppWeb(driver, phoneNumber, request.Message);
                        result.SuccessfulSends++;
                        _logger.LogInformation("Message sent to {PhoneNumber}", phoneNumber);
                        
                        // Reduced delay to 2-4 seconds for speed
                        await Task.Delay(Random.Shared.Next(2000, 4000));
                    }
                    catch (Exception ex)
                    {
                        result.FailedSends++;
                        result.FailedPhoneNumbers.Add(phoneNumber);
                        _logger.LogError(ex, "Failed to send message to {PhoneNumber}", phoneNumber);
                    }
                }

                result.Success = result.SuccessfulSends > 0;
                result.Message = result.Success 
                    ? $"Message sent successfully to {result.SuccessfulSends} users. Failed: {result.FailedSends}. WARNING: WhatsApp Web automation may result in account ban."
                    : "Failed to send message to any user.";

                // Log the broadcast
                await LogBroadcastAsync(request, result);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending WhatsApp broadcast via Web Automation");
                result.Success = false;
                result.Message = $"An error occurred: {ex.Message}";
                return result;
            }
            finally
            {
                try
                {
                    driver?.Quit();
                }
                catch { }
                try
                {
                    driver?.Dispose();
                }
                catch { }
                
                // Clean up profile directory
                if (!string.IsNullOrWhiteSpace(tempProfilePath) && Directory.Exists(tempProfilePath))
                {
                    try
                    {
                        Directory.Delete(tempProfilePath, recursive: true);
                        _logger.LogInformation("Cleaned up profile directory: {Path}", tempProfilePath);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Could not clean up profile directory: {Path}", tempProfilePath);
                    }
                }
            }
        }

        private async Task SendMessageViaWhatsAppWeb(IWebDriver driver, string phoneNumber, string message)
        {
            try
            {
                // Open chat with the phone number
                var url = $"https://web.whatsapp.com/send?phone={phoneNumber}&text={Uri.EscapeDataString(message)}";
                driver.Navigate().GoToUrl(url);
                _logger.LogInformation("Opened chat for {PhoneNumber}", phoneNumber);

                // Wait for page to load
                await Task.Delay(3000);

                // Check if number is not on WhatsApp - multiple error messages
                var errorSelectors = new[]
                {
                    "//div[contains(text(), 'Phone number shared via url is invalid')]",
                    "//div[contains(text(), 'not on WhatsApp')]",
                    "//div[contains(text(), 'This phone number')]",
                    "//div[contains(text(), 'invalid')]",
                    "//div[contains(text(), 'cannot be added')]"
                };

                foreach (var selector in errorSelectors)
                {
                    try
                    {
                        var errorElements = driver.FindElements(By.XPath(selector));
                        if (errorElements.Any(e => e.Displayed))
                        {
                            _logger.LogWarning("Number {PhoneNumber} is not on WhatsApp, skipping", phoneNumber);
                            throw new Exception("Number not on WhatsApp");
                        }
                    }
                    catch { }
                }

                // Wait for message input to be ready
                await Task.Delay(2000);

                // Try to find and click send button using JavaScript (more reliable)
                bool sent = false;
                for (int attempt = 1; attempt <= 5; attempt++)
                {
                    try
                    {
                        _logger.LogInformation("Send attempt {Attempt} for {PhoneNumber}", attempt, phoneNumber);
                        
                        // Try different selectors for send button
                        var sendSelectors = new[]
                        {
                            "//span[@data-icon='send']",
                            "//button[@data-testid='send']",
                            "//div[@data-testid='send']",
                            "//*[@data-icon='send']"
                        };

                        foreach (var selector in sendSelectors)
                        {
                            try
                            {
                                var sendButton = driver.FindElement(By.XPath(selector));
                                if (sendButton.Displayed && sendButton.Enabled)
                                {
                                    // Use JavaScript to click (more reliable)
                                    ((IJavaScriptExecutor)driver).ExecuteScript("arguments[0].click();", sendButton);
                                    sent = true;
                                    _logger.LogInformation("Message sent to {PhoneNumber} via {Selector}", phoneNumber, selector);
                                    break;
                                }
                            }
                            catch { }
                        }

                        if (sent) break;

                        // If button not found, try pressing Enter
                        if (!sent)
                        {
                            try
                            {
                                var messageBox = driver.FindElement(By.XPath("//div[@contenteditable='true']"));
                                messageBox.SendKeys(Keys.Enter);
                                sent = true;
                                _logger.LogInformation("Message sent to {PhoneNumber} via Enter key", phoneNumber);
                            }
                            catch { }
                        }

                        if (sent) break;

                        await Task.Delay(1000);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning("Attempt {Attempt} failed for {PhoneNumber}: {Error}", attempt, phoneNumber, ex.Message);
                        await Task.Delay(1000);
                    }
                }

                if (!sent)
                {
                    throw new Exception("Failed to send message after 5 attempts");
                }

                // Wait for message to be sent
                await Task.Delay(1500);
            }
            catch (Exception ex)
            {
                if (ex.Message == "Number not on WhatsApp")
                    throw;
                
                _logger.LogError(ex, "Failed to send message to {PhoneNumber}", phoneNumber);
                throw new Exception($"Failed to send message: {ex.Message}");
            }
        }

        private string FormatPhoneNumber(string phone)
        {
            if (string.IsNullOrWhiteSpace(phone))
                return null;

            // Remove all non-digit characters
            var digits = System.Text.RegularExpressions.Regex.Replace(phone, @"[^\d]", "");

            // Ensure it's a valid Indian number (10 digits)
            if (digits.Length != 10)
            {
                // If it's 11 digits and starts with 0, remove the 0
                if (digits.Length == 11 && digits.StartsWith("0"))
                {
                    digits = digits.Substring(1);
                }
                // If it's 12 digits and starts with 91, it's already with country code
                else if (digits.Length == 12 && digits.StartsWith("91"))
                {
                    digits = digits.Substring(2);
                }
                else
                {
                    return null; // Invalid format
                }
            }

            // Add +91 prefix
            return $"+91{digits}";
        }

        private async Task LogBroadcastAsync(WhatsAppBroadcastRequest request, WhatsAppBroadcastResult result)
        {
            try
            {
                var log = new Apilog
                {
                    Apiname = "WhatsAppBroadcast",
                    Reqdatae = DateTime.Now,
                    Request = JsonSerializer.Serialize(new
                    {
                        Message = request.Message,
                        SendToActiveUsersOnly = request.SendToActiveUsersOnly
                    }),
                    Response = JsonSerializer.Serialize(new
                    {
                        result.Success,
                        result.TotalUsers,
                        result.SuccessfulSends,
                        result.FailedSends,
                        result.Message,
                        result.SentAt
                    })
                };

                _context.Apilogs.Add(log);
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error logging WhatsApp broadcast");
            }
        }
    }
}

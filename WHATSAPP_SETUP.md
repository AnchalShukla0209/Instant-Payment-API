# WhatsApp Broadcast API - Web Automation Setup Guide

⚠️ **WARNING: This implementation uses WhatsApp Web Automation which violates WhatsApp's Terms of Service and may result in your WhatsApp account being permanently banned. Use at your own risk.**

## Important Warnings

### ⚠️ High Risk of Account Ban
- **WhatsApp actively detects and blocks automated browser usage**
- Your WhatsApp number may be **permanently banned** from using WhatsApp
- This method is **against WhatsApp's Terms of Service**
- Not recommended for production environments

### ⚠️ Reliability Issues
- WhatsApp Web automation is **unreliable** and may stop working anytime
- Requires **manual QR code scanning** for each session
- Browser updates can break the automation
- Slow performance (5-10 seconds per message)

### ⚠️ Technical Requirements
- Requires **Chrome browser** installed on the server
- Requires **continuous server connection** to maintain session
- High server resource usage (Chrome browser instance)
- Not suitable for cloud/serverless environments

## Configuration

The configuration in `appsettings.Development.json` and `appsettings.Production.json`:

```json
"WhatsApp": {
  "UseWebAutomation": true,
  "HeadlessMode": true,
  "ChromeBinaryPath": ""
}
```

- `UseWebAutomation`: Set to `true` to enable web automation
- `HeadlessMode`: Set to `true` to run Chrome without UI (recommended for servers)
- `ChromeBinaryPath`: Optional - Full path to chrome.exe if Chrome is not in default location (e.g., `C:\Program Files\Google\Chrome\Application\chrome.exe`)

## How It Works

1. **Initial Setup**: When the API is called, it launches Chrome browser
2. **QR Code Scan**: You have **30 seconds** to manually scan the QR code on WhatsApp Web
3. **Message Sending**: After login, it navigates to each chat and sends messages
4. **Rate Limiting**: Adds 5-10 second random delays between messages to avoid detection
5. **Cleanup**: Browser is automatically closed after sending all messages

## API Endpoint

**POST** `/api/WhatsApp/broadcast`

**Request Body:**
```json
{
  "message": "Your broadcast message here",
  "sendToActiveUsersOnly": true
}
```

**Response:**
```json
{
  "success": true,
  "message": "Message sent successfully to 50 users. Failed: 2. WARNING: WhatsApp Web automation may result in account ban.",
  "data": {
    "totalUsers": 52,
    "successfulSends": 50,
    "failedSends": 2,
    "failedPhoneNumbers": ["+919876543210", "+919876543211"],
    "sentAt": "2024-04-18T10:30:00Z"
  }
}
```

## NuGet Packages Required

The following packages have been added to `InstantPay.Application.csproj`:
- `Selenium.WebDriver` (4.20.0)
- `Selenium.Chrome.WebDriver` (2.45.0)

## Troubleshooting

### QR Code Not Scanning in Time
- Increase the delay in `WhatsAppService.cs` line 113 (currently 30 seconds)
- Set `HeadlessMode` to `false` in configuration to see the browser window

### Chrome Driver Issues
- Ensure Chrome browser is installed on the server
- Chrome driver version must match Chrome browser version
- The Selenium.WebDriver.ChromeDriver package should handle this automatically

### Server Deployment Issues
If the API works on localhost but fails on the server:

1. **Chrome Installation**: Ensure Google Chrome is installed on the server
   - Windows Server: Download and install Chrome from https://www.google.com/chrome/
   - Note the installation path (usually `C:\Program Files\Google\Chrome\Application\chrome.exe`)

2. **Set Chrome Binary Path**: If Chrome is not in the default location, update `appsettings.Production.json`:
   ```json
   "WhatsApp": {
     "UseWebAutomation": true,
     "HeadlessMode": true,
     "ChromeBinaryPath": "C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe"
   }
   ```

3. **Check Server Logs**: Review the application logs for detailed error messages:
   - ChromeDriver path
   - Chrome profile path
   - Specific error details

4. **Permission Issues**: Ensure the application has permission to:
   - Access Chrome binary
   - Write to temp directory (for Chrome profile)
   - Create and delete directories

5. **Common Server Errors**:
   - "Chrome failed to start" → Chrome not installed or wrong path
   - "Failed to write prefs file" → Permission issue with temp directory
   - "DevToolsActivePort file doesn't exist" → Chrome version mismatch with ChromeDriver

### Messages Not Sending
- Check if WhatsApp Web layout has changed (XPath selectors may need updating)
- Ensure phone numbers are in correct format (+91XXXXXXXXXX)
- Check logs for specific error messages

## Better Alternatives

For production use, consider these **safe and legal** alternatives:

### 1. Meta WhatsApp Business API (Recommended)
- **Official API** from Meta
- **Compliant** with WhatsApp Terms of Service
- **Reliable** and scalable
- **Cost**: Pay per message

### 2. Third-Party Providers
- **Twilio WhatsApp API** - Reliable, paid with free tier
- **MessageBird** - Professional WhatsApp API
- **Gupshup** - Enterprise WhatsApp solution

### 3. Use SMS Instead
- **MSG91** is already configured in your project
- **More reliable** than WhatsApp automation
- **Works with all phones** (no app required)
- **Cost**: Pay per SMS (usually cheaper than WhatsApp API)

### 4. Telegram Bot (Free Alternative)
- **Completely free** API
- **No user activation required**
- **Official API** (no risk of ban)
- **Downside**: Users need Telegram app

## Disclaimer

**This implementation is provided for educational/testing purposes only. The developers are not responsible for any WhatsApp account bans or legal consequences resulting from the use of this automation. Use at your own risk.**

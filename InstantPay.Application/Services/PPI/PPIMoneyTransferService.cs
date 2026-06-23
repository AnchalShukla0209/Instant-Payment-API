using System.Text;
using System.Text.Json;
using InstantPay.Application.DTOs;
using InstantPay.Application.Interfaces.PPI;
using InstantPay.Infrastructure.Sql.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace InstantPay.Application.Services.PPI;

public class PPIMoneyTransferService : IPPIMoneyTransferService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<PPIMoneyTransferService> _logger;
    private readonly AppDbContext _context;
    private readonly string _baseUrl;
    private readonly string _appId;
    private readonly string _authKey;
    private readonly string _secretKey;

    private const decimal MinAmount = 100m;
    private const decimal MaxAmount = 100000m;
    private const int ServiceId = 6;
    private static readonly object _txnLock = new();

    public PPIMoneyTransferService(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<PPIMoneyTransferService> logger,
        AppDbContext context)
    {
        _httpClient = httpClientFactory.CreateClient();
        _logger = logger;
        _context = context;

        var ppiConfig = configuration.GetSection("PPI");
        _baseUrl = ppiConfig["BaseUrl"] ?? "https://api.digikhata.in/p2a/";
        _appId = ppiConfig["AppId"] ?? string.Empty;
        _authKey = ppiConfig["AuthKey"] ?? string.Empty;
        _secretKey = ppiConfig["SecretKey"] ?? string.Empty;

        _httpClient.Timeout = TimeSpan.FromSeconds(30);
    }

    private static string GenerateClientId()
    {
        lock (_txnLock)
        {
            return "DMT"
                + DateTime.UtcNow.ToString("yyyyMMddHHmmssfff")
                + Guid.NewGuid().ToString("N").Substring(0, 6);
        }
    }

    public async Task<PPIMoneyTransferResponse> MoneyTransferAsync(PPIMoneyTransferRequest request)
    {
        try
        {
            if (request.APIKey != "PPI01")
                return Fail("Invalid API Key");

            if (!decimal.TryParse(request.Amount, out decimal amount))
                return Fail("Invalid amount format");

            if (amount < MinAmount || amount > MaxAmount)
                return Fail($"Please Enter Amount between {MinAmount:0} to {MaxAmount:0}");

            if (!int.TryParse(request.UserId, out int userId))
                return Fail("Invalid User ID");

            var user = await _context.TblUsers.FirstOrDefaultAsync(x => x.Id == userId);
            if (user == null)
                return Fail("User not found");

            // Duplicate transaction check
            var duplicate = await _context.TransactionDetails.AnyAsync(
                x => x.UserId == userId.ToString()
                  && x.Amount.ToString() == request.Amount
                  && x.AccountNo == request.AccountNo
                  && x.ServiceName == "DMT"
                  && x.OperatorName == "PPI"
                  && x.ReqDate >= DateTime.Now.AddSeconds(-150));

            if (duplicate)
                return Fail("Duplicate Transaction");

            string clientId = GenerateClientId();

            // Insert transaction record (Pending)
            var tx = new TransactionDetail
            {
                UserId = userId.ToString(),
                UserName = user.Name + "-" + user.Phone,
                WlId = user.Wlid,
                MdId = user.Mdid,
                AdId = user.Adid,
                TxnId = clientId,
                ServiceName = "DMT",
                OperatorName = "PPI",
                OpId = null,
                Mobileno = request.Sendermobile,
                OldBal = 0,
                Amount = Convert.ToDecimal(request.Amount),
                Comm = 0,
                Charge = 0,
                Cost = amount,
                NewBal = "0",
                Status = "Pending",
                Brid = null,
                TxnType = "Debit",
                ApiTxnId = clientId,
                ApiName = "PPI",
                AdminRemarks = null,
                ApiMsg = null,
                ApiRes = null,
                ApiReq = JsonSerializer.Serialize(new { request.Sendermobile, request.AccountNo, request.BeneId, request.Amount, request.TXNMode }),
                ReqDate = DateTime.Now,
                UpdateDate = DateTime.Now,
                CustomerName = request.BeneName,
                AccountNo = request.AccountNo,
                ComingFrom = request.ComingFrom,
                IfscCode = request.IfscCode,
                BankName = request.BankName,
                Tds = 0,
                TxnMode = request.TXNMode,
                MdComm = 0,
                AdComm = 0,
                WlComm = 0,
                ServiceId = ServiceId,
                SuperAdminShare = 0
            };

            await _context.TransactionDetails.AddAsync(tx);
            await _context.SaveChangesAsync();

            // Call PPI fund transfer API
            var payload = new
            {
                mobilenumber = request.Sendermobile,
                partnertxnrefid = clientId,
                txntype = request.TXNMode,
                beneficiaryid = request.BeneId,
                amount = request.Amount,
                otptoken = request.OtpToken,
                otp = request.OTP
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

            _logger.LogInformation("PPI MoneyTransfer | ClientId: {ClientId} Mobile: {Mobile} AccountNo: {AccountNo} Amount: {Amount}",
                clientId, request.Sendermobile, request.AccountNo, request.Amount);

            var response = await _httpClient.PostAsync($"{_baseUrl}v2/fundtransfer/validateotpanddofundtranfer", content);
            var responseContent = await response.Content.ReadAsStringAsync();

            _logger.LogInformation("PPI MoneyTransfer Response | ClientId: {ClientId} Response: {Response}", clientId, responseContent);

            tx.ApiRes = responseContent;
            tx.UpdateDate = DateTime.Now;

            if (!response.IsSuccessStatusCode)
            {
                tx.Status = "Failed";
                await _context.SaveChangesAsync();
                _logger.LogError("PPI MoneyTransfer HTTP error {StatusCode} | ClientId: {ClientId}", response.StatusCode, clientId);
                return Fail("Fund transfer failed. Please try again later.");
            }

            using var jsonDoc = JsonDocument.Parse(responseContent);
            var root = jsonDoc.RootElement;

            if (!root.TryGetProperty("resultCode", out var resultCode) ||
                !root.TryGetProperty("resultMessage", out var resultMessage))
            {
                tx.Status = "Pending";
                await _context.SaveChangesAsync();
                _logger.LogWarning("PPI MoneyTransfer unexpected response | ClientId: {ClientId}", clientId);
                return Fail("Unexpected response from fund transfer service");
            }

            if (resultCode.GetString() == "2000" && root.TryGetProperty("result", out var result))
            {
                string txnStatus = result.TryGetProperty("txnstatus", out var ts) ? ts.GetString()?.ToUpper() ?? "" : "";
                string txnRefId = result.TryGetProperty("txnreferenceid", out var tr) ? tr.GetString() ?? "" : "";
                string bankRRN = result.TryGetProperty("bankRRN", out var br) ? br.GetString() ?? "" : "";
                string message = resultMessage.GetString() ?? "Transaction processed";

                if (txnStatus == "SUCCESS" || txnStatus == "PENDING")
                {
                    tx.Status = txnStatus == "SUCCESS" ? "SUCCESS" : "PENDING";
                    tx.Brid = bankRRN;
                    tx.ApiTxnId = txnRefId;
                    await _context.SaveChangesAsync();

                    var txnRecord = new PPIMoneyTransferTransaction
                    {
                        AccountNo = request.AccountNo,
                        BeneName = request.BeneName,
                        Amount = request.Amount,
                        Charge = "0.00",
                        CurrentBalance = "0.00",
                        Status = tx.Status,
                        TxnID = txnRefId,
                        BR_Id = bankRRN,
                        TxnDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")
                    };

                    return new PPIMoneyTransferResponse
                    {
                        Status_Code = "1",
                        Message = "Transaction Successful.",
                        Data = new List<PPIMoneyTransferTransaction> { txnRecord }
                    };
                }
                else
                {
                    tx.Status = "Failed";
                    await _context.SaveChangesAsync();
                    return Fail(message);
                }
            }
            else
            {
                tx.Status = "Failed";
                await _context.SaveChangesAsync();
                return Fail(resultMessage.GetString() ?? "Fund transfer failed");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in PPI MoneyTransfer");
            return Fail("An unexpected error occurred.");
        }
    }

    private static PPIMoneyTransferResponse Fail(string message) =>
        new() { Status_Code = "0", Message = message, Data = message };
}

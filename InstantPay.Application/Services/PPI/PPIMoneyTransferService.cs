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

    private static string GenerateClientId() =>
        "DMT" + DateTime.UtcNow.ToString("yyyyMMddHHmmssfff") + Guid.NewGuid().ToString("N")[..6];

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

            string clientId;
            TransactionDetail tx;

            await using var dbTx = await _context.Database.BeginTransactionAsync(CancellationToken.None);
            try
            {
                string appLockName = $"PPI_{userId}_{request.AccountNo}_{request.Amount}";
                int lockResult = (await _context.Database
                    .SqlQueryRaw<int>(
                        "DECLARE @r INT; EXEC @r = sp_getapplock @Resource = {0}, @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 5000; SELECT @r",
                        appLockName)
                    .ToListAsync()).First();

                if (lockResult < 0)
                {
                    await dbTx.RollbackAsync(CancellationToken.None);
                    return Fail("Transaction already in progress, please try again");
                }

                // Duplicate transaction check
                var duplicate = await _context.TransactionDetails.AnyAsync(
                    x => x.UserId == userId.ToString()
                      && x.Amount.ToString() == request.Amount
                      && x.AccountNo == request.AccountNo
                      && x.ServiceName == "DMT"
                      && x.OperatorName == "PPI"
                      && x.ReqDate >= DateTime.Now.AddSeconds(-150));

                if (duplicate)
                {
                    await dbTx.RollbackAsync(CancellationToken.None);
                    return Fail("Duplicate Transaction");
                }

                clientId = GenerateClientId();

                // Insert transaction record (Pending)
                tx = new TransactionDetail
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
                await dbTx.CommitAsync(CancellationToken.None);
            }
            catch
            {
                await dbTx.RollbackAsync(CancellationToken.None);
                throw;
            }

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

            _logger.LogInformation("PPI MoneyTransfer | ClientId: {ClientId} Mobile: {Mobile} AccountNo: {AccountNo} Amount: {Amount}",
                clientId, request.Sendermobile, request.AccountNo, request.Amount);

            using var httpReq = CreatePpiRequest("v2/fundtransfer/validateotpanddofundtranfer", content, request.TokeyKey);
            var response = await _httpClient.SendAsync(httpReq);
            var responseContent = await response.Content.ReadAsStringAsync();

            _logger.LogInformation("PPI MoneyTransfer Response | ClientId: {ClientId} Response: {Response}", clientId, responseContent);

            tx.ApiRes = responseContent;
            tx.UpdateDate = DateTime.Now;

            if (!response.IsSuccessStatusCode)
            {
                var log = new Apilog
                {
                    Apiname = "PPI-FundTransfer",
                    Reqdatae = DateTime.Now,
                    Request = JsonSerializer.Serialize(payload),
                    Response = responseContent + "||" + response.StatusCode
                };
                _context.Apilogs.Add(log);
                await _context.SaveChangesAsync();

                tx.Status = "Failed";
                await _context.SaveChangesAsync();
                _logger.LogError("PPI MoneyTransfer HTTP error {StatusCode} | ClientId: {ClientId}", response.StatusCode, clientId);
                return Fail("Fund transfer failed. Please try again later.");
            }

            var logs = new Apilog
            {
                Apiname = "PPI-FundTransfer",
                Reqdatae = DateTime.Now,
                Request = JsonSerializer.Serialize(payload),
                Response = responseContent + "||" + response.StatusCode
            };
            _context.Apilogs.Add(logs);
            await _context.SaveChangesAsync();

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

            if (resultCode.GetRawText().Trim('"') == "2000" && root.TryGetProperty("result", out var result))
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
        new() { Status_Code = "0", Message = message, Data = string.Empty };
}

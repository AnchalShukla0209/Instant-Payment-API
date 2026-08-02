using InstantPay.Application.DTOs;
using InstantPay.Application.Interfaces;
using InstantPay.Application.Interfaces.PPI;
using InstantPay.Infrastructure.Sql.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;

namespace InstantPay.Application.Services.PPI;

public class PPIWalletService : IPPIWalletService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<PPIWalletService> _logger;
    private readonly IWalletService _walletService;
    private readonly ICommissionService _commissionService;
    private readonly AppDbContext _context;
    private readonly string _baseUrl;
    private readonly string _appId;
    private readonly string _authKey;
    private readonly string _secretKey;
    private const int ServiceId = 6;
    private const string ApiCode = "PPI";

    public PPIWalletService(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<PPIWalletService> logger,
        IWalletService walletService,
        ICommissionService commissionService,
        AppDbContext context)
    {
        _httpClient = httpClientFactory.CreateClient();
        _logger = logger;
        _walletService = walletService;
        _commissionService = commissionService;
        _context = context;

        // Load PPI configuration from appsettings
        var ppiConfig = configuration.GetSection("PPI");
        _baseUrl = ppiConfig["BaseUrl"] ?? "https://api.digikhata.in/p2a/";
        _appId = ppiConfig["AppId"] ?? "INSTANTPAYMENT";
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

    private async Task<decimal> GetCommissionFromPlanAsync(int planId, decimal amount, string shareColumn)
        => await _commissionService.GetCommissionFromPlanAsync(planId, amount, ServiceId, ApiCode, shareColumn);

    private Task DistributeCommissionAsync(TransactionDetail tx, TblUser user, decimal amount, int planId)
        => _commissionService.DistributeCommissionAsync(
            tx, user, amount, planId, ServiceId, ApiCode,
            $"Commission Credit PPI Wallet Load | Credit by Services");

    public bool VerifyPin(string inputPin, string txnPin)
    {
        return inputPin.ToLower().Trim() == txnPin.ToLower().Trim();
    }

    public async Task<PPILoadWalletResponse> LoadWalletAsync(PPILoadWalletRequest request)
    {
        try
        {
            // Validate API Key
            if (request.APIKey != "PPI01")
            {
                _logger.LogWarning("Invalid API Key provided for PPI Load Wallet");
                return new PPILoadWalletResponse
                {
                    Status_Code = "0",
                    Message = "Invalid API Key",
                    Data = new()
                };
            }

            // Validate amount range
            if (decimal.TryParse(request.Amount, out decimal amount))
            {
                if (amount < 5000 || amount > 100000)
                {
                    _logger.LogWarning("Invalid amount {Amount} for PPI Load Wallet. Amount must be between 5000 and 100000", amount);
                    return new PPILoadWalletResponse
                    {
                        Status_Code = "0",
                        Message = "Please Enter Amount between 5000 to 100000",
                        Data = new()
                    };
                }
            }
            else
            {
                _logger.LogWarning("Invalid amount format: {Amount}", request.Amount);
                return new PPILoadWalletResponse
                {
                    Status_Code = "0",
                    Message = "Invalid amount format",
                    Data = new()
                };
            }

            // Get user
            if (!int.TryParse(request.UserId, out int userId))
            {
                _logger.LogWarning("Invalid UserId: {UserId}", request.UserId);
                return new PPILoadWalletResponse
                {
                    Status_Code = "0",
                    Message = "Invalid User ID",
                    Data = new()
                };
            }

            var user = await _context.TblUsers.FirstOrDefaultAsync(x => x.Id == userId);
            if (user == null)
            {
                _logger.LogWarning("User not found: {UserId}", userId);
                return new PPILoadWalletResponse
                {
                    Status_Code = "0",
                    Message = "User not found",
                    Data = new()
                };
            }

            if (!VerifyPin(request.TxnPin, user.TxnPin))
            {
                return new PPILoadWalletResponse
                {
                    Status_Code = "0",
                    Message = "Invalid Txn Pin",
                    Data = new()
                };
            }

            decimal rtComm = 0m;
            decimal totalDebit = 0m;
            decimal newBal = 0m;
            string txnId = string.Empty;
            int planId = 0;
            TransactionDetail tx = null!;

            await using var dbTx = await _context.Database.BeginTransactionAsync(CancellationToken.None);
            try
            {
                string appLockName = $"PPIW_{user.Id}_{request.Sendermobile}_{request.Amount}";
                int lockResult = (await _context.Database
                    .SqlQueryRaw<int>(
                        "DECLARE @r INT; EXEC @r = sp_getapplock @Resource = {0}, @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 5000; SELECT @r",
                        appLockName)
                    .ToListAsync()).First();

                if (lockResult < 0)
                {
                    await dbTx.RollbackAsync(CancellationToken.None);
                    _logger.LogWarning("Lock timeout for user {UserId}", userId);
                    return new PPILoadWalletResponse
                    {
                        Status_Code = "0",
                        Message = "Transaction already in progress, please try again",
                        Data = new()
                    };
                }

                // Duplicate transaction check
                var duplicate = await _context.TransactionDetails.AnyAsync(
                    x => x.UserId == user.Id.ToString()
                      && x.Amount.ToString() == request.Amount.ToString()
                      && x.ServiceName == "DMT"
                      && x.OperatorName == "PPI Wallet Load"
                      && x.ReqDate >= DateTime.Now.AddSeconds(-150));

                if (duplicate)
                {
                    await dbTx.RollbackAsync(CancellationToken.None);
                    _logger.LogWarning("Duplicate transaction detected for user {UserId}", userId);
                    return new PPILoadWalletResponse
                    {
                        Status_Code = "0",
                        Message = "Duplicate Transaction",
                        Data = new()
                    };
                }

                // Get current balance
                decimal currentBalance = await _walletService.GetBalanceAsync(userId);
                planId = user.CommissionPlanId ?? 1;
                rtComm = await GetCommissionFromPlanAsync(planId, amount, "RT");
                totalDebit = amount + rtComm;

                if (currentBalance < totalDebit)
                {
                    await dbTx.RollbackAsync(CancellationToken.None);
                    _logger.LogWarning("Insufficient balance for user {UserId}. Current: {Current}, Required: {Required}",
                        userId, currentBalance, totalDebit);
                    return new PPILoadWalletResponse
                    {
                        Status_Code = "0",
                        Message = "Insufficient Balance",
                        Data = new()
                    };
                }

                // Generate transaction ID
                txnId = GenerateClientId();
                newBal = currentBalance - totalDebit;

                // Create transaction record
                tx = new TransactionDetail
                {
                    UserId = Convert.ToString(user.Id),
                    UserName = user.Name + "-" + user.Phone,
                    WlId = user.Wlid,
                    MdId = user.Mdid,
                    AdId = user.Adid,
                    TxnId = txnId,
                    ServiceName = "DMT",
                    OperatorName = "PPI Wallet Load",
                    OpId = null,
                    Mobileno = request.Sendermobile,
                    OldBal = currentBalance,
                    Amount = Convert.ToDecimal(request.Amount),
                    Comm = 0,
                    Charge = Convert.ToDecimal(rtComm),
                    Cost = totalDebit,
                    NewBal = Convert.ToString(newBal),
                    Status = "Pending",
                    Brid = null,
                    TxnType = "Debit",
                    ApiTxnId = txnId,
                    ApiName = "PPI",
                    AdminRemarks = null,
                    ApiMsg = null,
                    ApiRes = null,
                    ApiReq = JsonSerializer.Serialize(request),
                    ReqDate = DateTime.Now,
                    UpdateDate = DateTime.Now,
                    CustomerName = user.Name,
                    AccountNo = request.Sendermobile,
                    ComingFrom = request.ComingFrom,
                    IfscCode = null,
                    BankName = null,
                    Tds = 0,
                    TxnMode = null,
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

            // Pre-debit: debit wallet before calling the API.
            // If debit fails for any reason, abort immediately — no API call is made.
            try
            {
                var (_, _, debitEntryId) = await _walletService.DebitAsync(
                    userId,
                    user.Username + "-" + user.Phone,
                    amount,
                    totalDebit,
                    rtComm,
                    0,
                    "PPI_Wallet_Load",
                    $"PPI Wallet Load for Mobile {request.Sendermobile} | Debit by Services | {txnId}",
                    user.Wlid);

                // Solid check: SELECT to confirm the debit row was persisted before calling the API.
                bool debitVerified = debitEntryId > 0
                    && await _context.Tbluserbalances.AnyAsync(
                           b => b.Id == debitEntryId && b.UserId == userId);

                if (!debitVerified)
                {
                    tx.Status = "FAILED";
                    tx.ApiMsg = "Wallet debit failed before API call";
                    tx.UpdateDate = DateTime.Now;
                    try { await _context.SaveChangesAsync(CancellationToken.None); } catch { }

                    return new PPILoadWalletResponse
                    {
                        Status_Code = "0",
                        Message = "Please try again later, there is an issue with your wallet",
                        Data = new()
                    };
                }
            }
            catch (Exception debitEx)
            {
                tx.Status = "Failed";
                tx.ApiMsg = "Wallet debit failed before API call";
                tx.UpdateDate = DateTime.Now;
                try { await _context.SaveChangesAsync(); } catch { }
                _logger.LogError(debitEx, "PPI Wallet debit failed for user {UserId}, txnId {TxnId}", userId, txnId);
                return new PPILoadWalletResponse
                {
                    Status_Code = "0",
                    Message = "Please try again later, there is an issue with your wallet",
                    Data = new()
                };
            }

            // Prepare PPI API payload
            var payload = new
            {
                outletcode = "C3",
                partnertxnrefId = txnId,
                mobileno = request.Sendermobile,
                txnamount = request.Amount
            };

            var jsonPayload = JsonSerializer.Serialize(payload);
            var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

            _logger.LogInformation("Sending PPI Load Wallet request for Mobile: {Mobile}, Amount: {Amount}, TxnId: {TxnId}", 
                request.Sendermobile, request.Amount, txnId);

            // Make the API call
            using var httpReq = CreatePpiRequest("v1/wallet/loadwallet", content, request.TokeyKey);
            var response = await _httpClient.SendAsync(httpReq);
            var responseContent = await response.Content.ReadAsStringAsync();
            _logger.LogInformation("PPI Load Wallet API Response: {Response}", responseContent);

            // Update transaction with API response
            tx.ApiRes = responseContent;
            tx.UpdateDate = DateTime.Now;
            await _context.SaveChangesAsync();

            if (!response.IsSuccessStatusCode)
            {

                // Log to apilogs table
                var logs = new Apilog
                {
                    Apiname = "PPI-LoadWallet",
                    Reqdatae = DateTime.Now,
                    Request = jsonPayload,
                    Response = responseContent + "||" + response.StatusCode
                };
                _context.Apilogs.Add(logs);
                await _context.SaveChangesAsync();

                // Refund user on HTTP failure
                await _walletService.CreditAsync(
                    userId,
                    user.Username + "-" + user.Phone,
                    amount,
                    totalDebit,
                    rtComm,
                    0,
                    "PPI_Wallet_Load_Refund",
                    $"PPI Wallet Load Refund for Mobile {request.Sendermobile} | Credit by Services",
                    user.Wlid);

                tx.Status = "Failed";
                tx.Brid = txnId;
                await _context.SaveChangesAsync();

                return new PPILoadWalletResponse
                {
                    Status_Code = "0",
                    Message = "Failed to load wallet. Please try again later.",
                    Data = new()
                };
            }

            var log = new Apilog
            {
                Apiname = "PPI-LoadWallet",
                Reqdatae = DateTime.Now,
                Request = jsonPayload,
                Response = responseContent + "||" + response.StatusCode
            };
            _context.Apilogs.Add(log);
            await _context.SaveChangesAsync();
            // Parse the response
            using var jsonDoc = JsonDocument.Parse(responseContent);
            var root = jsonDoc.RootElement;

            if (root.TryGetProperty("resultCode", out var resultCode) && 
                root.TryGetProperty("resultMessage", out var resultMessage))
            {
                // Check resultStatus - only refund if FAILED
                string resultStatus = root.TryGetProperty("resultStatus", out var rStatus) ? rStatus.GetString() ?? "" : "";
                

                if (resultCode.GetRawText().Trim('"') == "2000" && resultStatus != "FAILED")
                {
                    tx.Status = "Success";
                    tx.Brid = txnId;
                    await _context.SaveChangesAsync();

                    // Distribute commission
                    await DistributeCommissionAsync(tx, user, amount, planId);

                    // Build response
                    var transaction = new PPILoadWalletTransaction
                    {
                        AccountNo = request.Sendermobile,
                        BeneName = user.Name,
                        Amount = request.Amount,
                        Charge = rtComm.ToString("0.00"),
                        CurrentBalance = newBal.ToString("0.00"),
                        Status = "Success",
                        TxnID = txnId,
                        BR_Id = txnId,
                        TxnDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")
                    };

                    return new PPILoadWalletResponse
                    {
                        Status_Code = "1",
                        Message = "Transaction Successful.",
                        Data = new List<PPILoadWalletTransaction> { transaction }
                    };
                }
                else if (resultStatus == "FAILED")
                {
                    // Refund user only if resultStatus is FAILED
                    await _walletService.CreditAsync(
                        userId,
                        user.Username + "-" + user.Phone,
                        amount,
                        totalDebit,
                        rtComm,
                        0,
                        "PPI_Wallet_Load_Refund",
                        $"PPI Wallet Load Refund for Mobile {request.Sendermobile} | Credit by Services",
                        user.Wlid);

                    tx.Status = "Failed";
                    await _context.SaveChangesAsync();

                    return new PPILoadWalletResponse
                    {
                        Status_Code = "0",
                        Message = resultMessage.GetString() ?? "Failed to load wallet",
                        Data = new()
                    };
                }
                else
                {
                    // resultCode != 2000 but resultStatus is not FAILED - don't refund
                    tx.Status = "Pending";
                    tx.Brid = txnId;
                    await _context.SaveChangesAsync();

                    var transaction = new PPILoadWalletTransaction
                    {
                        AccountNo = request.Sendermobile,
                        BeneName = user.Name,
                        Amount = request.Amount,
                        Charge = rtComm.ToString("0.00"),
                        CurrentBalance = newBal.ToString("0.00"),
                        Status = "Pending",
                        TxnID = txnId,
                        BR_Id = txnId,
                        TxnDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")
                    };

                    return new PPILoadWalletResponse
                    {
                        Status_Code = "1",
                        Message = "Transaction Successful.",
                        Data = new List<PPILoadWalletTransaction> { transaction }
                    };
                }
            }

            // Fallback for unexpected response format - don't refund
            _logger.LogWarning("Unexpected PPI Load Wallet API response format: {Response}", responseContent);
            
            tx.Status = "Pending";
            await _context.SaveChangesAsync();

            return new PPILoadWalletResponse
            {
                Status_Code = "0",
                Message = "Unexpected response from wallet load service",
                Data = new()
            };
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error occurred while calling PPI Load Wallet API");
            return new PPILoadWalletResponse
            {
                Status_Code = "0",
                Message = "Network error. Please check your connection.",
                Data = new()
            };
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogError(ex, "Request timeout while calling PPI Load Wallet API");
            return new PPILoadWalletResponse
            {
                Status_Code = "0",
                Message = "Request timeout. Please try again.",
                Data = new()
            };
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Error parsing PPI Load Wallet API response");
            return new PPILoadWalletResponse
            {
                Status_Code = "0",
                Message = "Error processing response.",
                Data = new()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in PPI Load Wallet");
            return new PPILoadWalletResponse
            {
                Status_Code = "0",
                Message = "An unexpected error occurred.",
                Data = new()
            };
        }
    }
}

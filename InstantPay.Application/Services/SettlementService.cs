using InstantPay.Application.DTOs;
using InstantPay.Application.Interfaces;
using InstantPay.Application.Interfaces.RBL;
using InstantPay.Application.Services.RBL;
using InstantPay.Infrastructure.Sql.Entities;
using InstantPay.SharedKernel.Entity.RechargeKitConfigDTO;
using InstantPay.SharedKernel.Entity.RblConfigDTO;
using InstantPay.SharedKernel.Results.MoneyTransfer.RechargeKit;
using InstantPay.SharedKernel.Results.MoneyTransfer.RBL;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Globalization;
using System.Net.Http.Headers;

namespace InstantPay.Application.Services
{
    public class SettlementService : ISettlementService
    {
        private readonly AppDbContext _context;
        private readonly RechargeKitConfig _config;
        private readonly RblConfig _rblConfig;
        private readonly IWalletService _walletService;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<SettlementService> _logger;
        private readonly IRblOpenSslTransport _rblTransport;

        public SettlementService(AppDbContext context, IOptions<RechargeKitConfig> config,
            IOptions<RblConfig> rblConfig, IWalletService walletService,
            IHttpClientFactory httpClientFactory, ILogger<SettlementService> logger,
            IRblOpenSslTransport rblTransport)
        {
            _context = context;
            _config = config.Value;
            _rblConfig = rblConfig.Value;
            _walletService = walletService;
            _httpClientFactory = httpClientFactory;
            _logger = logger;
            _rblTransport = rblTransport;
        }

        private decimal CalculateCharge(decimal amount, string withdrawalType)
        {
            if (withdrawalType.ToUpper() == "AEPS")
            {
                return 10; // Flat 10 rs for AEPS
            }
            else if (withdrawalType.ToUpper() == "MATM")
            {
                return 10; // Flat 10 rs for MATM
            }
            else if (withdrawalType.ToUpper() == "RAZORPAY")
            {
                if (amount <= 50000)
                {
                    return 50; // 50 rs flat till 50K
                }
                else if (amount <= 100000)
                {
                    return 100; // 100 rs flat till 1 lac
                }
                else
                {
                    return 100; // For amounts above 1 lac, still 100 rs (or adjust as needed)
                }
            }
            return 0;
        }

        public async Task<SettlementDto> GetSettlementAsync(string? userId = null)
        {
            try
            {
                // Calculate date range: last 2 days excluding today
                var today = DateTime.Today;
                var fromDate = today.AddDays(-2); // 2 days before today
                var toDate = today.AddDays(-1);   // Yesterday

                // Query AEPS transactions for last 2 days (excluding today)
                var aepsQuery = _context.TransactionDetails
                    .Where(t =>
                        (t.OperatorName == "AEPS_CASH_WITHDRAWAL" || t.OperatorName == "CW" || t.OperatorName== "FINO_AEPS_CASH_WITHDRAWAL") &&
                        t.ServiceName == "AEPS" &&
                        t.Status == "SUCCESS" &&
                        t.ReqDate.HasValue &&
                        t.ReqDate.Value.Date >= fromDate &&
                        t.ReqDate.Value.Date <= toDate);

                // Query MATM transactions for last 2 days (excluding today)
                var matmQuery = _context.TransactionDetails
                    .Where(t =>
                        t.OperatorName == "CW" &&
                        t.ServiceName == "MATM" &&
                        t.Status == "SUCCESS" &&
                        t.ReqDate.HasValue &&
                        t.ReqDate.Value.Date >= fromDate &&
                        t.ReqDate.Value.Date <= toDate);

                // Query Razorpay transactions for last 2 days including today
                var razorpayQuery = _context.Tblonlinepayments
                    .Where(p =>
                        p.Gatwaytype == "Razorpay" &&
                        p.Status == "SUCCESS" &&
                        p.ReqDate.HasValue &&
                        p.ReqDate.Value.Date >= fromDate &&
                        p.ReqDate.Value.Date <= today);

                // Filter by userId if provided
                if (!string.IsNullOrEmpty(userId))
                {
                    aepsQuery = aepsQuery.Where(t => t.UserId == userId);
                    matmQuery = matmQuery.Where(t => t.UserId == userId);
                    razorpayQuery = razorpayQuery.Where(p => p.UserKey == userId);
                }

                var aepsTransactions = await aepsQuery
                    .OrderByDescending(t => t.TransId)
                    .ToListAsync();

                var matmTransactions = await matmQuery
                    .OrderByDescending(t => t.TransId)
                    .ToListAsync();

                var razorpayTransactions = await razorpayQuery
                    .OrderByDescending(p => p.Id)
                    .ToListAsync();

                // Group by UserId for AEPS
                var aepsByUser = aepsTransactions
                    .GroupBy(t => t.UserId ?? "Unknown")
                    .Select(g => new
                    {
                        UserId = g.Key,
                        UserName = g.FirstOrDefault()?.UserName ?? "",
                        TotalAmount = g.Sum(t => t.Amount ?? 0)
                    })
                    .ToDictionary(x => x.UserId, x => (x.UserName, x.TotalAmount));

                // Group by UserId for MATM
                var matmByUser = matmTransactions
                    .GroupBy(t => t.UserId ?? "Unknown")
                    .Select(g => new
                    {
                        UserId = g.Key,
                        UserName = g.FirstOrDefault()?.UserName ?? "",
                        TotalAmount = g.Sum(t => t.Amount ?? 0)
                    })
                    .ToDictionary(x => x.UserId, x => (x.UserName, x.TotalAmount));

                // Group by UserKey for Razorpay
                var razorpayByUser = razorpayTransactions
                    .GroupBy(p => p.UserKey ?? "Unknown")
                    .Select(g => new
                    {
                        UserId = g.Key,
                        UserName = g.FirstOrDefault()?.UserName ?? "",
                        TotalAmount = g.Sum(p => p.TransferAmt ?? 0)
                    })
                    .ToDictionary(x => x.UserId, x => (x.UserName, x.TotalAmount));

                // Get all unique users
                var allUsers = aepsByUser.Keys.Union(matmByUser.Keys).Union(razorpayByUser.Keys).ToList();

                // Get withdrawal amounts from SettlementWithdrawals table
                var withdrawals = await GetWithdrawalsAsync(fromDate, toDate, today, userId);

                var userSettlements = new List<UserSettlementDetail>();

                foreach (var user in allUsers)
                {
                    var aepsData = aepsByUser.ContainsKey(user) ? aepsByUser[user] : ("", 0);
                    var matmData = matmByUser.ContainsKey(user) ? matmByUser[user] : ("", 0);
                    var razorpayData = razorpayByUser.ContainsKey(user) ? razorpayByUser[user] : ("", 0);
                    var userName = !string.IsNullOrEmpty(aepsData.UserName) ? aepsData.UserName : 
                                   (!string.IsNullOrEmpty(matmData.UserName) ? matmData.UserName : razorpayData.UserName);
                    
                    var aepsAmount = aepsData.TotalAmount;
                    var matmAmount = matmData.TotalAmount;
                    var razorpayAmount = razorpayData.TotalAmount;
                    var aepsWithdrawn = withdrawals.ContainsKey(user) ? withdrawals[user].AEPSWithdrawn : 0;
                    var matmWithdrawn = withdrawals.ContainsKey(user) ? withdrawals[user].MATMWithdrawn : 0;
                    var razorpayWithdrawn = withdrawals.ContainsKey(user) ? withdrawals[user].RazorpayWithdrawn : 0;

                    userSettlements.Add(new UserSettlementDetail
                    {
                        UserId = user,
                        UserName = userName,
                        AEPSAmount = aepsAmount,
                        MATMAmount = matmAmount,
                        RazorpayAmount = razorpayAmount,
                        AEPSWithdrawn = aepsWithdrawn,
                        MATMWithdrawn = matmWithdrawn,
                        RazorpayWithdrawn = razorpayWithdrawn,
                        AvailableAEPS = aepsAmount - aepsWithdrawn,
                        AvailableMATM = matmAmount - matmWithdrawn,
                        AvailableRazorpay = razorpayAmount - razorpayWithdrawn
                    });
                }

                // Calculate totals
                var totalAEPS = aepsByUser.Values.Sum(x => x.TotalAmount);
                var totalMATM = matmByUser.Values.Sum(x => x.TotalAmount);
                var totalRazorpay = razorpayByUser.Values.Sum(x => x.TotalAmount);
                var totalAEPSWithdrawn = userSettlements.Sum(u => u.AEPSWithdrawn);
                var totalMATMWithdrawn = userSettlements.Sum(u => u.MATMWithdrawn);
                var totalRazorpayWithdrawn = userSettlements.Sum(u => u.RazorpayWithdrawn);

                return new SettlementDto
                {
                    UserId = userId ?? "All Users",
                    TotalAEPSAmount = totalAEPS,
                    TotalMATMAmount = totalMATM,
                    TotalRazorpayAmount = totalRazorpay,
                    AEPSWithdrawnAmount = totalAEPSWithdrawn,
                    MATMWithdrawnAmount = totalMATMWithdrawn,
                    RazorpayWithdrawnAmount = totalRazorpayWithdrawn,
                    AvailableAEPSAmount = totalAEPS - totalAEPSWithdrawn,
                    AvailableMATMAmount = totalMATM - totalMATMWithdrawn,
                    AvailableRazorpayAmount = totalRazorpay - totalRazorpayWithdrawn,
                    SettlementFromDate = fromDate,
                    SettlementToDate = toDate,
                    UserSettlements = userSettlements
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in GetSettlementAsync: {ex.Message}");
                throw;
            }
        }

        public async Task<WithdrawalResponseDto> WithdrawAmountAsync(WithdrawalRequestDto request)
        {
            try
            {
                // Calculate date range for validation
                var today = DateTime.Today;
                var fromDate = today.AddDays(-2);
                var toDate = today.AddDays(-1);
                var razorpayToDate = today;

                // Get current settlement data for the user
                var settlement = await GetSettlementAsync(request.UserId);

                var userSettlement = settlement.UserSettlements
                    .FirstOrDefault(u => u.UserId == request.UserId);

                var userData = await _context.TblUsers.Where(x => x.Id == Convert.ToInt32(request.UserId)).FirstOrDefaultAsync();
                if (userData == null)
                {
                    return new WithdrawalResponseDto
                    {
                        Success = false,
                        Message = "user not found",
                        RemainingAmount = 0,
                        NewWalletBalance = 0,
                        Charge = 0
                    };
                }

                if (userSettlement == null)
                {
                    return new WithdrawalResponseDto
                    {
                        Success = false,
                        Message = "User not found in settlement data",
                        RemainingAmount = 0,
                        NewWalletBalance = 0,
                        Charge = 0
                    };
                }

                request.BeneAddress = "";
                request.BenePhone = userData.Phone ?? "";
                request.BeneEmail =  "";
                request.Latitude = userData.Lat ?? "76.7887";
                request.Longitude = userData.Longitute ?? "78.7654";

                decimal availableAmount = 0;
                decimal remainingAmount = 0;

                if (request.WithdrawalType.ToUpper() == "AEPS")
                {
                    availableAmount = userSettlement.AvailableAEPS;
                    if (request.Amount > availableAmount)
                    {
                        return new WithdrawalResponseDto
                        {
                            Success = false,
                            Message = $"Insufficient AEPS balance. Available: {availableAmount}, Requested: {request.Amount}",
                            RemainingAmount = availableAmount,
                            NewWalletBalance = 0,
                            Charge = 0
                        };
                    }
                    remainingAmount = availableAmount - request.Amount;
                }
                else if (request.WithdrawalType.ToUpper() == "MATM")
                {
                    availableAmount = userSettlement.AvailableMATM;
                    if (request.Amount > availableAmount)
                    {
                        return new WithdrawalResponseDto
                        {
                            Success = false,
                            Message = $"Insufficient MATM balance. Available: {availableAmount}, Requested: {request.Amount}",
                            RemainingAmount = availableAmount,
                            NewWalletBalance = 0,
                            Charge = 0
                        };
                    }
                    remainingAmount = availableAmount - request.Amount;
                }
                else if (request.WithdrawalType.ToUpper() == "RAZORPAY")
                {
                    availableAmount = userSettlement.AvailableRazorpay;
                    if (request.Amount > availableAmount)
                    {
                        return new WithdrawalResponseDto
                        {
                            Success = false,
                            Message = $"Insufficient Razorpay balance. Available: {availableAmount}, Requested: {request.Amount}",
                            RemainingAmount = availableAmount,
                            NewWalletBalance = 0,
                            Charge = 0
                        };
                    }
                    remainingAmount = availableAmount - request.Amount;
                }
                else
                {
                    return new WithdrawalResponseDto
                    {
                        Success = false,
                        Message = "Invalid withdrawal type. Use 'AEPS', 'MATM', or 'Razorpay'",
                        RemainingAmount = 0,
                        NewWalletBalance = 0,
                        Charge = 0
                    };
                }

                // Get user's current wallet balance
                var userIdInt = int.TryParse(request.UserId, out var parsedId) ? parsedId : 0;
                if (userIdInt == 0)
                {
                    return new WithdrawalResponseDto
                    {
                        Success = false,
                        Message = "Invalid UserId format",
                        RemainingAmount = availableAmount,
                        NewWalletBalance = 0,
                        Charge = 0
                    };
                }

                decimal currentWalletBalance = await _walletService.GetBalanceAsync(userIdInt);

                // Calculate charge
                var charge = CalculateCharge(request.Amount, request.WithdrawalType);
                var totalDebit = request.Amount + charge;

                if (totalDebit > currentWalletBalance)
                {
                    return new WithdrawalResponseDto
                    {
                        Success = false,
                        Message = $"Insufficient wallet balance. Wallet Balance: {currentWalletBalance}, Required: {totalDebit} (Amount: {request.Amount} + Charge: {charge})",
                        RemainingAmount = availableAmount,
                        NewWalletBalance = currentWalletBalance,
                        Charge = charge
                    };
                }

                // Validate beneficiary details for payout
                if (string.IsNullOrEmpty(request.BankAccount) || string.IsNullOrEmpty(request.Ifsc) || string.IsNullOrEmpty(request.BeneName))
                {
                    return new WithdrawalResponseDto
                    {
                        Success = false,
                        Message = "Bank account, IFSC, and beneficiary name are required for payout",
                        RemainingAmount = availableAmount,
                        NewWalletBalance = currentWalletBalance,
                        Charge = charge
                    };
                }

                string clientReferenceId = string.Empty;
                var payoutProvider = await ResolveSettlementProviderAsync();
                if (string.IsNullOrEmpty(payoutProvider))
                {
                    return new WithdrawalResponseDto
                    {
                        Success = false,
                        Message = "Settlement payout provider configuration is invalid. Enable exactly one of RKIT or RBL.",
                        RemainingAmount = availableAmount,
                        NewWalletBalance = currentWalletBalance,
                        Charge = charge
                    };
                }
                SettlementWithdrawal withdrawal = null!;
                string Username = userData.Name + "-" + userData.Phone;
                var recordToDate = request.WithdrawalType.ToUpper() == "RAZORPAY" ? razorpayToDate : toDate;

                await using var settlementDbTx = await _context.Database.BeginTransactionAsync(CancellationToken.None);
                try
                {
                    string appLockName = $"SETT_{request.UserId}_{request.BankAccount}_{request.Amount}_{request.WithdrawalType}";
                    int lockResult = (await _context.Database
                        .SqlQueryRaw<int>(
                            "DECLARE @r INT; EXEC @r = sp_getapplock @Resource = {0}, @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 5000; SELECT @r",
                            appLockName)
                        .ToListAsync()).First();

                    if (lockResult < 0)
                    {
                        await settlementDbTx.RollbackAsync(CancellationToken.None);
                        return new WithdrawalResponseDto
                        {
                            Success = false,
                            Message = "Transaction already in progress, please try again",
                            RemainingAmount = availableAmount,
                            NewWalletBalance = currentWalletBalance,
                            Charge = charge
                        };
                    }

                    // Check 1: 5-minute check for same amount with same account
                    var fiveMinutesAgo = DateTime.Now.AddMinutes(-5);
                    var recentDuplicate = await _context.SettlementWithdrawals
                        .Where(w => w.UserId == request.UserId &&
                                    w.BankAccount == request.BankAccount &&
                                    w.Amount == request.Amount &&
                                    w.WithdrawalDate >= fiveMinutesAgo)
                        .FirstOrDefaultAsync();

                    if (recentDuplicate != null)
                    {
                        await settlementDbTx.RollbackAsync(CancellationToken.None);
                        return new WithdrawalResponseDto
                        {
                            Success = false,
                            Message = $"A withdrawal of {request.Amount} to the same bank account was processed within the last 5 minutes. Please wait before trying again.",
                            RemainingAmount = availableAmount,
                            NewWalletBalance = currentWalletBalance,
                            Charge = charge
                        };
                    }

                    clientReferenceId = payoutProvider == "RBL"
                        ? $"TXN{Random.Shared.Next(0x8000000):X7}"
                        : $"SETT_{userIdInt}_{DateTime.Now:yyyyMMddHHmmss}";

                    // Check 2: Exact duplicate transaction check
                    var exactDuplicate = await _context.SettlementWithdrawals
                        .Where(w => w.UserId == request.UserId &&
                                    w.BankAccount == request.BankAccount &&
                                    w.Ifsc == request.Ifsc &&
                                    w.Amount == request.Amount &&
                                    w.WithdrawalType.ToUpper() == request.WithdrawalType.ToUpper() &&
                                    w.BeneName == request.BeneName && w.PayoutTransactionId == clientReferenceId)
                        .FirstOrDefaultAsync();

                    if (exactDuplicate != null)
                    {
                        await settlementDbTx.RollbackAsync(CancellationToken.None);
                        return new WithdrawalResponseDto
                        {
                            Success = false,
                            Message = $"A duplicate transaction with the same details already exists. Transaction ID: {exactDuplicate.Id}, Date: {exactDuplicate.WithdrawalDate}",
                            RemainingAmount = availableAmount,
                            NewWalletBalance = currentWalletBalance,
                            Charge = charge
                        };
                    }

                    // Insert a pending settlement withdrawal record before calling the payout API
                    withdrawal = await CreatePendingSettlementWithdrawalAsync(request, fromDate, recordToDate, charge, clientReferenceId, Username);
                    await settlementDbTx.CommitAsync(CancellationToken.None);
                }
                catch
                {
                    await settlementDbTx.RollbackAsync(CancellationToken.None);
                    throw;
                }

                // Debit user wallet before calling the payout API (same process as RechargeKitDmtService)
                int debitEntryId = 0;
                decimal newWalletBalance = currentWalletBalance;
                try
                {
                    (_, newWalletBalance, debitEntryId) = await _walletService.DebitAsync(
                        userIdInt,
                        Username,
                        request.Amount, totalDebit, charge, 0,
                        "SETTLEMENT_WITHDRAWAL",
                        $"{request.WithdrawalType} Settlement Withdrawal || {clientReferenceId}",
                        userData.Wlid);
                }
                catch (Exception ex)
                {
                    withdrawal.PayoutStatus = "FAILED";
                    withdrawal.ApiMsg = "Wallet debit failed before API call: " + ex.Message;
                    withdrawal.WithdrawalDate = DateTime.Now;
                    await _context.SaveChangesAsync();
                    return new WithdrawalResponseDto
                    {
                        Success = false,
                        Message = "Please try again later, there is an issue with your wallet",
                        RemainingAmount = availableAmount,
                        NewWalletBalance = currentWalletBalance,
                        Charge = charge
                    };
                }

                bool debitVerified = debitEntryId > 0
                    && await _context.Tbluserbalances.AnyAsync(b => b.Id == debitEntryId && b.UserId == userIdInt);

                if (!debitVerified)
                {
                    withdrawal.PayoutStatus = "FAILED";
                    withdrawal.ApiMsg = "Debit entry not found in tbluserbalance after insert";
                    withdrawal.WithdrawalDate = DateTime.Now;
                    await _context.SaveChangesAsync();
                    return new WithdrawalResponseDto
                    {
                        Success = false,
                        Message = "Debit entry not found in tbluserbalance after insert",
                        RemainingAmount = availableAmount,
                        NewWalletBalance = currentWalletBalance,
                        Charge = charge
                    };
                }

                var payoutResponse = payoutProvider switch
                {
                    "RBL" => await CallRblPayoutApi(request, clientReferenceId),
                    "RKIT" => await CallRechargeKitPayoutApi(request, clientReferenceId),
                    _ => new ProviderPayoutResult { Status = "PENDING", Message = "No settlement payout provider is enabled" }
                };

                // Update withdrawal with API response (brid, request, response, message, status)
                UpdateSettlementWithdrawalWithApiResponse(withdrawal, payoutResponse);

                // Refund wallet on payout failure
                if (payoutResponse.Status == "FAILED")
                {
                    await RefundSettlementAsync(withdrawal, userData, request, charge);
                }

                await _context.SaveChangesAsync();

                bool isSuccessOrPending = payoutResponse.Status != "FAILED";
                return new WithdrawalResponseDto
                {
                    Success = isSuccessOrPending,
                    Message = isSuccessOrPending
                        ? $"Withdrawal of {request.Amount} from {request.WithdrawalType} initiated. Charge: {charge}"
                        : $"Payout failed: {payoutResponse.Message}",
                    RemainingAmount = remainingAmount,
                    NewWalletBalance = newWalletBalance,
                    Charge = charge,
                    PayoutTransactionId = payoutResponse.TransactionId,
                    PayoutStatus = payoutResponse.Status
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in WithdrawAmountAsync: {ex.Message}");
                return new WithdrawalResponseDto
                {
                    Success = false,
                    Message = $"Error processing withdrawal: {ex.Message}",
                    RemainingAmount = 0,
                    NewWalletBalance = 0,
                    Charge = 0
                };
            }
        }
      
        private async Task<Dictionary<string, (decimal AEPSWithdrawn, decimal MATMWithdrawn, decimal RazorpayWithdrawn)>> GetWithdrawalsAsync(
            DateTime fromDate, DateTime toDate, DateTime razorpayToDate, string? userId = null)
        {
            try
            {
                // Get AEPS withdrawals for last 2 days (excluding today)
                var aepsQuery = _context.SettlementWithdrawals
                    .Where(w => w.WithdrawalType.ToUpper() == "AEPS" &&
                                w.SettlementFromDate == fromDate &&
                                w.SettlementToDate == toDate &&
                                (w.PayoutStatus.Trim().ToUpper() == "SUCCESS" || w.PayoutStatus.Trim().ToUpper() == "PENDING"));

                // Get MATM withdrawals for last 2 days (excluding today)
                var matmQuery = _context.SettlementWithdrawals
                    .Where(w => w.WithdrawalType.ToUpper() == "MATM" &&
                                w.SettlementFromDate == fromDate &&
                                w.SettlementToDate == toDate &&
                                (w.PayoutStatus.Trim().ToUpper() == "SUCCESS" || w.PayoutStatus.Trim().ToUpper() == "PENDING"));

                // Get Razorpay withdrawals for last 2 days including today
                var razorpayQuery = _context.SettlementWithdrawals
                    .Where(w => w.WithdrawalType.ToUpper() == "RAZORPAY" &&
                                w.SettlementFromDate == fromDate &&
                                w.SettlementToDate == razorpayToDate &&
                                (w.PayoutStatus.Trim().ToUpper() == "SUCCESS" || w.PayoutStatus.Trim().ToUpper() == "PENDING"));

                if (!string.IsNullOrEmpty(userId))
                {
                    aepsQuery = aepsQuery.Where(w => w.UserId == userId);
                    matmQuery = matmQuery.Where(w => w.UserId == userId);
                    razorpayQuery = razorpayQuery.Where(w => w.UserId == userId);
                }

                var aepsWithdrawals = await aepsQuery
                    .GroupBy(w => w.UserId)
                    .Select(g => new
                    {
                        UserId = g.Key,
                        AEPSWithdrawn = g.Sum(w => w.Amount)
                    })
                    .ToDictionaryAsync(x => x.UserId, x => x.AEPSWithdrawn);

                var matmWithdrawals = await matmQuery
                    .GroupBy(w => w.UserId)
                    .Select(g => new
                    {
                        UserId = g.Key,
                        MATMWithdrawn = g.Sum(w => w.Amount)
                    })
                    .ToDictionaryAsync(x => x.UserId, x => x.MATMWithdrawn);

                var razorpayWithdrawals = await razorpayQuery
                    .GroupBy(w => w.UserId)
                    .Select(g => new
                    {
                        UserId = g.Key,
                        RazorpayWithdrawn = g.Sum(w => w.Amount)
                    })
                    .ToDictionaryAsync(x => x.UserId, x => x.RazorpayWithdrawn);

                // Merge results
                var allUsers = aepsWithdrawals.Keys.Union(matmWithdrawals.Keys).Union(razorpayWithdrawals.Keys).ToList();
                var result = new Dictionary<string, (decimal, decimal, decimal)>();

                foreach (var user in allUsers)
                {
                    var aepsWithdrawn = aepsWithdrawals.ContainsKey(user) ? aepsWithdrawals[user] : 0;
                    var matmWithdrawn = matmWithdrawals.ContainsKey(user) ? matmWithdrawals[user] : 0;
                    var razorpayWithdrawn = razorpayWithdrawals.ContainsKey(user) ? razorpayWithdrawals[user] : 0;
                    result[user] = (aepsWithdrawn, matmWithdrawn, razorpayWithdrawn);
                }

                return result;
            }
            catch (Exception ex)
            {
                // Log error here
                Console.WriteLine($"Error fetching withdrawals: {ex.Message}");
                return new Dictionary<string, (decimal, decimal, decimal)>();
            }
        }

        private async Task<SettlementWithdrawal> CreatePendingSettlementWithdrawalAsync(WithdrawalRequestDto request, DateTime fromDate, DateTime toDate, decimal charge, string clientReferenceId, string Username)
        {
            var withdrawal = new SettlementWithdrawal
            {
                UserId = request.UserId,
                Amount = request.Amount,
                Charge = charge,
                WithdrawalType = request.WithdrawalType ?? "",
                WithdrawalDate = DateTime.Now,
                SettlementFromDate = fromDate,
                SettlementToDate = toDate,
                Remarks = $"Withdrawal of {request.Amount} from {request.WithdrawalType}. Charge: {charge}",
                BankAccount = request.BankAccount ?? "",
                Ifsc = request.Ifsc ?? "",
                BeneName = request.BeneName ?? "",
                BeneEmail = request.BeneEmail ?? "",
                BenePhone = request.BenePhone ?? "",
                BeneAddress = request.BeneAddress ?? "",
                Latitude = request.Latitude ?? "",
                Longitude = request.Longitude ?? "",
                PayoutTransactionId = clientReferenceId,
                PayoutReferenceId = "",
                PayoutStatus = "PENDING",
                PayoutResponse = "",
                ApiRequest = "",
                ApiMsg = "",
                CreatedAt = DateTime.Now,
                UserName = Username,
                RRN = "",
                BankName = request.BankName ?? "",
                ComingFrom = request.ComingFrom ?? ""
            };

            _context.SettlementWithdrawals.Add(withdrawal);
            await _context.SaveChangesAsync();
            return withdrawal;
        }

        private static void UpdateSettlementWithdrawalWithApiResponse(SettlementWithdrawal withdrawal, ProviderPayoutResult payoutResponse)
        {
            withdrawal.PayoutReferenceId = payoutResponse.TransactionId ?? "";
            withdrawal.RRN = payoutResponse.ReferenceId ?? "";
            withdrawal.PayoutResponse = payoutResponse.RawResponse ?? "";
            withdrawal.ApiRequest = payoutResponse.RequestJson ?? "";
            withdrawal.ApiMsg = payoutResponse.Message ?? "";
            withdrawal.PayoutStatus = payoutResponse.Status;
            withdrawal.WithdrawalDate = DateTime.Now;
        }

        private async Task RefundSettlementAsync(SettlementWithdrawal withdrawal, TblUser user, WithdrawalRequestDto request, decimal charge)
        {
            try
            {
                decimal totalRefund = request.Amount + charge;
                var (_, _, creditEntryId) = await _walletService.CreditAsync(
                    user.Id, user.Name + "-" + user.Phone,
                    request.Amount, totalRefund, charge, 0,
                    "SettlementWithdrawal_Refund",
                    $"Settlement Withdrawal Refunded | Account No {request.BankAccount} | Refund Credit TXN:{withdrawal.PayoutTransactionId}",
                    user.Wlid);

                bool creditVerified = creditEntryId > 0
                    && await _context.Tbluserbalances.AnyAsync(b => b.Id == creditEntryId && b.UserId == user.Id);

                if (!creditVerified)
                {
                    withdrawal.PayoutStatus = "PENDING";
                    withdrawal.ApiMsg = "Refund credit not verified — kept as PENDING for retry";
                    withdrawal.WithdrawalDate = DateTime.Now;
                }
            }
            catch (Exception ex)
            {
                withdrawal.PayoutStatus = "PENDING";
                withdrawal.ApiMsg = $"Refund failed: {ex.Message} — kept as PENDING for retry";
                withdrawal.WithdrawalDate = DateTime.Now;
            }
        }

        private static string MapPayoutStatus(int status, string optransid)
        {
            return status switch
            {
                1 => string.IsNullOrWhiteSpace(optransid) ? "PENDING" : "SUCCESS",
                2 => "PENDING",
                3 => "FAILED",
                _ => "PENDING"
            };
        }

        private async Task<ProviderPayoutResult> CallRechargeKitPayoutApi(WithdrawalRequestDto request, string partnerRequestId, CancellationToken cancellationToken = default)
        {
            var bodyObj = new
            {
                mobile_no = !string.IsNullOrWhiteSpace(request.BenePhone) ? request.BenePhone : "8684020633",
                account_no = request.BankAccount,
                ifsc = request.Ifsc,
                bank_name = request.BankName,
                beneficiary_name = request.BeneName,
                amount = request.Amount,
                transfer_type = _config.TransferType ?? "5",
                partner_request_id = partnerRequestId
            };

            string json = JsonConvert.SerializeObject(bodyObj);
            byte[] jsonBytes = Encoding.UTF8.GetBytes(json);

            try
            {
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

                var handler = new SocketsHttpHandler
                {
                    UseProxy = false,
                    AutomaticDecompression = DecompressionMethods.None,
                    ConnectCallback = async (context, ct) =>
                    {
                        var addresses = await Dns.GetHostAddressesAsync(context.DnsEndPoint.Host, ct);
                        var ipv4 = addresses.First(a => a.AddressFamily == AddressFamily.InterNetwork);
                        var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                        await socket.ConnectAsync(ipv4, context.DnsEndPoint.Port, ct);
                        return new NetworkStream(socket, ownsSocket: true);
                    }
                };

                using var client = new HttpClient(handler);
                var httpRequest = new HttpRequestMessage(HttpMethod.Post, _config.PayoutUrl);
                httpRequest.Content = new ByteArrayContent(jsonBytes);
                httpRequest.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
                httpRequest.Headers.Add("Authorization", $"Bearer {_config.BearerToken}");

                var response = await client.SendAsync(httpRequest, cancellationToken);
                string resp = await response.Content.ReadAsStringAsync(cancellationToken);

                var log = new Apilog
                {
                    Apiname = "Settlement-RKIT",
                    Reqdatae = DateTime.Now,
                    Request = json,
                    Response = resp
                };
                _context.Apilogs.Add(log);
                await _context.SaveChangesAsync(cancellationToken);

                var apiResponse = JsonConvert.DeserializeObject<RechargeKitPayoutApiResponse>(resp);

                if (apiResponse == null)
                {
                    return new ProviderPayoutResult
                    {
                        Status = "FAILED",
                        Message = "Invalid or empty API response",
                        RawResponse = resp,
                        RequestJson = json
                    };
                }

                string mappedStatus = MapPayoutStatus(apiResponse.status, apiResponse.optransid ?? "");

                return new ProviderPayoutResult
                {
                    Status = mappedStatus,
                    Message = apiResponse.msg,
                    TransactionId = apiResponse.orderid ?? "",
                    ReferenceId = apiResponse.optransid ?? "",
                    RawResponse = resp,
                    RequestJson = json
                };
            }
            catch (Exception ex)
            {
                return new ProviderPayoutResult
                {
                    Status = "FAILED",
                    Message = $"API call failed: {ex.Message}",
                    RequestJson = json
                };
            }
        }

        private async Task<string> ResolveSettlementProviderAsync()
        {
            var mappings = await _context.SERVICE_PROVIDER.AsNoTracking()
                .Where(x => x.ServiceCode != null && x.ServiceCode.ToUpper() == "SETTLEMENT")
                .Select(x => new { x.ProviderCode, x.IsEnabled })
                .ToListAsync();

            // Keep existing deployments working until SETTLEMENT mappings are seeded.
            if (mappings.Count == 0) return "RKIT";

            var enabled = mappings
                .Where(x => x.IsEnabled == true && !string.IsNullOrWhiteSpace(x.ProviderCode))
                .Select(x => x.ProviderCode!.Trim().ToUpperInvariant())
                .ToList();

            if (enabled.Count != 1)
            {
                _logger.LogError("Settlement provider configuration is invalid. Enabled providers: {Providers}", string.Join(",", enabled));
                return string.Empty;
            }

            return enabled[0] is "RKIT" or "RBL" ? enabled[0] : string.Empty;
        }

        private async Task<ProviderPayoutResult> CallRblPayoutApi(
            WithdrawalRequestDto request, string transactionId, CancellationToken cancellationToken = default)
        {
            var payload = new
            {
                Single_Payment_Corp_Req = new
                {
                    Header = new
                    {
                        TranID = transactionId,
                        Corp_ID = _rblConfig.CorpId,
                        Maker_ID = _rblConfig.MakerId,
                        Checker_ID = _rblConfig.CheckerId,
                        Approver_ID = _rblConfig.ApproverId
                    },
                    Body = new
                    {
                        Amount = request.Amount.ToString("0.##", CultureInfo.InvariantCulture),
                        Debit_Acct_No = _rblConfig.DebitAccountNumber,
                        Debit_Acct_Name = _rblConfig.DebitAccountName,
                        Debit_IFSC = _rblConfig.DebitIfsc,
                        Debit_Mobile = _rblConfig.DebitMobile,
                        Debit_TrnParticulars = "Settlement Payment",
                        Debit_PartTrnRmks = "Settlement Payout",
                        Ben_IFSC = request.Ifsc,
                        Ben_Acct_No = request.BankAccount,
                        Ben_Name = request.BeneName,
                        Ben_Address = string.IsNullOrWhiteSpace(request.BeneAddress) ? "India" : request.BeneAddress,
                        Ben_BankName = RblPayloadNormalizer.NormalizeBankName(request.BankName),
                        Ben_BankCd = "0",
                        Ben_BranchCd = "0",
                        Ben_Email = request.BeneEmail,
                        Ben_Mobile = request.BenePhone,
                        Ben_TrnParticulars = "Settlement Transfer",
                        Ben_PartTrnRmks = "Received",
                        Issue_BranchCd = "0000",
                        Mode_of_Pay = "IMPS",
                        Remarks = "DMR",
                        RptCode = "HSBA"
                    },
                    Signature = new { Signature = "Settlement Txn" }
                }
            };

            var json = JsonConvert.SerializeObject(payload);
            try
            {
                var uri = new UriBuilder(_rblConfig.PaymentUrl)
                {
                    Query = $"client_id={Uri.EscapeDataString(_rblConfig.ClientId)}&client_secret={Uri.EscapeDataString(_rblConfig.ClientSecret)}"
                }.Uri;
                var response = await _rblTransport.PostAsync(uri.ToString(), json, cancellationToken);
                var responseText = response.Body;
                _context.Apilogs.Add(new Apilog
                {
                    Apiname = "Settlement-RBL",
                    Reqdatae = DateTime.Now,
                    Request = Truncate(json, 4000),
                    Response = Truncate($"HTTP {response.StatusCode} | {responseText}", 4000)
                });
                await _context.SaveChangesAsync(CancellationToken.None);

                if (response.StatusCode is < 200 or >= 300)
                {
                    _logger.LogWarning("RBL settlement returned HTTP {StatusCode} for {TransactionId}", response.StatusCode, transactionId);
                    var rejected = response.StatusCode is >= 400 and < 500
                        && response.StatusCode is not 408 and not 425;
                    return new ProviderPayoutResult {
                        Status = rejected ? "FAILED" : "PENDING",
                        Message = rejected ? ExtractRblGatewayError(responseText) : $"RBL HTTP {response.StatusCode}",
                        RawResponse = responseText, RequestJson = json };
                }

                var apiResponse = JsonConvert.DeserializeObject<RblPaymentResponse>(responseText);
                if (apiResponse?.Payment == null)
                {
                    var gatewayError = ExtractRblGatewayError(responseText);
                    return new ProviderPayoutResult { Status = string.IsNullOrWhiteSpace(gatewayError) ? "PENDING" : "FAILED",
                        Message = string.IsNullOrWhiteSpace(gatewayError) ? "Invalid or empty RBL response" : gatewayError,
                        RawResponse = responseText, RequestJson = json };
                }

                var header = apiResponse.Payment.Header;
                var body = apiResponse.Payment.Body;
                var success = string.Equals(header?.Status, "Success", StringComparison.OrdinalIgnoreCase) && header?.Resp_cde == "00";
                var uncertainFailure = header?.Error_Cde is "ER004" or "ER006" or "ER017" or "ER018";
                return new ProviderPayoutResult
                {
                    Status = success ? "SUCCESS" : uncertainFailure ? "PENDING" : "FAILED",
                    Message = success ? "Success" : header?.Error_Desc ?? "RBL payout failed",
                    TransactionId = body?.RefNo ?? transactionId,
                    ReferenceId = body?.RRN ?? body?.channelpartnerrefno ?? string.Empty,
                    RawResponse = responseText,
                    RequestJson = json
                };
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
            {
                _logger.LogError(ex, "RBL settlement transport/parse failure for {TransactionId}", transactionId);
                return new ProviderPayoutResult
                {
                    Status = "PENDING",
                    Message = "RBL API outcome unknown; kept pending for reconciliation",
                    RequestJson = json
                };
            }
        }

        private static string Truncate(string? value, int maxLength) =>
            string.IsNullOrEmpty(value) ? string.Empty : value.Length <= maxLength ? value : value[..maxLength];

        private static string ExtractRblGatewayError(string responseText)
        {
            if (string.IsNullOrWhiteSpace(responseText)) return string.Empty;
            try
            {
                var error = JsonConvert.DeserializeObject<Dictionary<string, object?>>(responseText);
                var message = error != null && error.TryGetValue("error", out var value) ? value?.ToString() : null;
                if (!string.IsNullOrWhiteSpace(message)) return $"RBL gateway rejected request: {message}";
            }
            catch (JsonException) { }
            return responseText.Contains("access denied", StringComparison.OrdinalIgnoreCase)
                ? "RBL gateway rejected request: access denied"
                : string.Empty;
        }

        private class ProviderPayoutResult
        {
            public string Status { get; set; } = "FAILED";
            public string Message { get; set; } = "API call failed";
            public string TransactionId { get; set; } = string.Empty;
            public string ReferenceId { get; set; } = string.Empty;
            public string RawResponse { get; set; } = string.Empty;
            public string RequestJson { get; set; } = string.Empty;
        }
    }
}

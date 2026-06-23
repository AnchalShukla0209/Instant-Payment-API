using InstantPay.Application.DTOs;
using InstantPay.Application.Interfaces;
using InstantPay.Infrastructure.Sql.Entities;
using Microsoft.EntityFrameworkCore;

namespace InstantPay.Application.Services
{
    public class SettlementService : ISettlementService
    {
        private readonly AppDbContext _context;
        private readonly IAeronpayPayoutService _payoutService;
        private readonly IWalletService _walletService;

        public SettlementService(AppDbContext context, IAeronpayPayoutService payoutService, IWalletService walletService)
        {
            _context = context;
            _payoutService = payoutService;
            _walletService = walletService;
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

                request.BeneAddress = userData.AddressLine1 + " " + userData.AddressLine2;
                request.BenePhone = userData.Phone ?? "";
                request.BeneEmail = userData.EmailId ?? "";
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
                    return new WithdrawalResponseDto
                    {
                        Success = false,
                        Message = $"A withdrawal of {request.Amount} to the same bank account was processed within the last 5 minutes. Please wait before trying again.",
                        RemainingAmount = availableAmount,
                        NewWalletBalance = currentWalletBalance,
                        Charge = charge
                    };
                }

                var clientReferenceId = $"SETT_{userIdInt}_{DateTime.Now:yyyyMMddHHmmss}";

                // Check 2: Exact duplicate transaction check
                var exactDuplicate = await _context.SettlementWithdrawals
                    .Where(w => w.UserId == request.UserId &&
                                w.BankAccount == request.BankAccount &&
                                w.Ifsc == request.Ifsc &&
                                w.Amount == request.Amount &&
                                w.WithdrawalType.ToUpper() == request.WithdrawalType.ToUpper() &&
                                w.BeneName == request.BeneName && w.PayoutTransactionId== clientReferenceId)
                    .FirstOrDefaultAsync();

                if (exactDuplicate != null)
                {
                    return new WithdrawalResponseDto
                    {
                        Success = false,
                        Message = $"A duplicate transaction with the same details already exists. Transaction ID: {exactDuplicate.Id}, Date: {exactDuplicate.WithdrawalDate}",
                        RemainingAmount = availableAmount,
                        NewWalletBalance = currentWalletBalance,
                        Charge = charge
                    };
                }

                // Process withdrawal with payout using transaction
                using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    // Call Aeronpay payout API
                    var payoutRequest = new AeronpayPayoutRequest
                    {
                        AccountNumber = "99760187733",
                        Amount = request.WithdrawalType.ToUpper() == "AEPS" || request.WithdrawalType.ToUpper() == "MATM"? request.Amount - charge : request.Amount,
                        ClientReferenceId = clientReferenceId,
                        Latitude = userData.Lat ?? "76.7887",
                        Longitude = userData.Longitute ?? "78.7654",
                        BankAccount = request.BankAccount,
                        Ifsc = request.Ifsc,
                        BeneName = request.BeneName,
                        BeneEmail = userData.EmailId ?? "",
                        BenePhone = userData.Phone ?? "",
                        BeneAddress = userData.AddressLine1 + " " + userData.AddressLine2
                    };

                    var payoutResponse = await _payoutService.ProcessPayoutAsync(payoutRequest);
                    decimal newWalletBalance = currentWalletBalance - totalDebit;
                    payoutResponse.Status = payoutResponse?.Status?.ToUpper() == "BAD_REQUEST_ERROR" ? "FAILED" : payoutResponse?.Status;
                    if (payoutResponse?.Status?.ToUpper() != "FALSE" && payoutResponse?.Status?.ToUpper() != "FAILED")
                    {
                        // Atomically debit withdrawal amount from user wallet (race-condition-safe via WalletService)
                        (_, newWalletBalance, _) = await _walletService.DebitAsync(
                            userIdInt,
                            userData.Name + "-" + userData.Phone,
                            request.Amount, totalDebit, charge, 0,
                            "SETTLEMENT_WITHDRAWAL",
                            $"{request.WithdrawalType} Settlement Withdrawal || {payoutRequest.ClientReferenceId}",
                            "1");
                    }
                    // Record settlement withdrawal with payout details
                    string Username = userData.Name + "-" + userData.Phone;
                    var recordToDate = request.WithdrawalType.ToUpper() == "RAZORPAY" ? razorpayToDate : toDate;
                    await RecordWithdrawalAsync(request, fromDate, recordToDate, charge, payoutResponse, clientReferenceId, Username);

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    return new WithdrawalResponseDto
                    {
                        Success = payoutResponse.Success,
                        Message = payoutResponse.Success 
                            ? $"Withdrawal of {request.Amount} from {request.WithdrawalType} successful. Charge: {charge}"
                            : $"Withdrawal recorded but payout failed: {payoutResponse.Message}",
                        RemainingAmount = remainingAmount,
                        NewWalletBalance = newWalletBalance,
                        Charge = charge,
                        PayoutTransactionId = payoutResponse.TransactionId,
                        PayoutStatus = payoutResponse.Status
                    };
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    Console.WriteLine($"Transaction failed: {ex.Message}");
                    throw;
                }
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

        private async Task RecordWithdrawalAsync(WithdrawalRequestDto request, DateTime fromDate, DateTime toDate, decimal charge, AeronpayPayoutResponse payoutResponse, string txnid, string Username)
        {
            try
            {
                var withdrawal = new SettlementWithdrawal
                {
                    UserId = request.UserId,
                    Amount = request.Amount,
                    Charge = charge,
                    WithdrawalType = request.WithdrawalType??"",
                    WithdrawalDate = DateTime.Now,
                    SettlementFromDate = fromDate,
                    SettlementToDate = toDate,
                    Remarks = $"Withdrawal of {request.Amount} from {request.WithdrawalType}. Charge: {charge}",
                    BankAccount = request.BankAccount??"",
                    Ifsc = request.Ifsc??"",
                    BeneName = request.BeneName??"",
                    BeneEmail = request.BeneEmail??"",
                    BenePhone = request.BenePhone??"",
                    BeneAddress = request.BeneAddress??"",
                    Latitude = request.Latitude ??"",
                    Longitude = request.Longitude??"",
                    PayoutTransactionId = txnid??"",
                    PayoutReferenceId = payoutResponse.TransactionId??"",
                    PayoutStatus = payoutResponse.Status =="False"?"FAILED": payoutResponse.Status ?? "",
                    PayoutResponse = payoutResponse.RawResponse??"",
                    CreatedAt = DateTime.Now,
                    UserName = Username,
                    RRN = "",
                    BankName= request.BankName ?? "",
                    ComingFrom = request.ComingFrom ?? ""
                };

                _context.SettlementWithdrawals.Add(withdrawal);
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                // Log error here
                Console.WriteLine($"Error recording withdrawal: {ex.Message}");
                throw;
            }
        }
    }
}

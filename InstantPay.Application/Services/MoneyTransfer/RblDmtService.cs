using InstantPay.Application.Interfaces;
using InstantPay.Application.Interfaces.MoneyTransfer.RBL;
using InstantPay.Application.Interfaces.SMS;
using InstantPay.Infrastructure.Sql.Entities;
using InstantPay.SharedKernel.Entity.RblConfigDTO;
using InstantPay.SharedKernel.RequestPayload.MoneyTransfer.AeronPay;
using InstantPay.SharedKernel.Results.MoneyTransfer.Castler;
using InstantPay.SharedKernel.Results.MoneyTransfer.RBL;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using System.Globalization;
using System.Net.Http.Headers;
using System.Text;

namespace InstantPay.Application.Services.MoneyTransfer;

public sealed class RblDmtService : IRblDmtService
{
    private const int ServiceId = 6;
    private const string ApiCode = "RBL";
    private readonly RblConfig _config;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly AppDbContext _context;
    private readonly IWalletService _walletService;
    private readonly ICommissionService _commissionService;
    private readonly ISmsService _smsService;
    private readonly ILogger<RblDmtService> _logger;

    public RblDmtService(IOptions<RblConfig> config, IHttpClientFactory httpClientFactory, AppDbContext context,
        IWalletService walletService, ICommissionService commissionService, ISmsService smsService,
        ILogger<RblDmtService> logger)
    {
        _config = config.Value;
        _httpClientFactory = httpClientFactory;
        _context = context;
        _walletService = walletService;
        _commissionService = commissionService;
        _smsService = smsService;
        _logger = logger;
    }

    public async Task<LoginModel> MoneyTransfer(AeronpayDmtRequest model, string ip, CancellationToken cancellationToken)
    {
        try
        {


            if (!int.TryParse(model.UserId, out var userId)) return Fail("Invalid User Id");
            if (!Validate(model, out var validationMessage)) return Fail(validationMessage);

            var user = await _context.TblUsers.FirstOrDefaultAsync(x => x.Id == userId, cancellationToken);
            if (user == null) return Fail("User not found");
            if (!string.Equals(model.TransactionPin?.Trim(), user.TxnPin?.Trim(), StringComparison.OrdinalIgnoreCase))
                return Fail("Invalid Pin");

            var amount = model.Amount!.Value;
            TransactionDetail tx;
            decimal charge;
            decimal totalDebit;
            decimal newBalance;
            int planId;
            string transactionId;

            await using (var dbTx = await _context.Database.BeginTransactionAsync(cancellationToken))
            {
                try
                {
                    var lockName = $"DMT_{user.Id}_{model.AccountNumber}_{amount}";
                    var lockResult = (await _context.Database.SqlQueryRaw<int>(
                        "DECLARE @r INT; EXEC @r = sp_getapplock @Resource = {0}, @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 5000; SELECT @r",
                        lockName).ToListAsync(cancellationToken)).First();
                    if (lockResult < 0) return await RollbackAndFail(dbTx, "Transaction already in progress, please try again", cancellationToken);

                    var duplicate = await _context.TransactionDetails.AnyAsync(x =>
                        x.UserId == user.Id.ToString() && x.Amount == amount &&
                        x.AccountNo == model.AccountNumber && x.ServiceName == "DMT" &&
                        x.ReqDate >= DateTime.Now.AddSeconds(-150), cancellationToken);
                    if (duplicate) return await RollbackAndFail(dbTx, "Duplicate Transaction", cancellationToken);

                    var currentBalance = await _walletService.GetBalanceAsync(user.Id, cancellationToken);
                    planId = user.CommissionPlanId ?? 1;
                    charge = await _commissionService.GetCommissionFromPlanAsync(planId, amount, ServiceId, ApiCode, "RT");
                    totalDebit = amount + charge;
                    if (currentBalance < totalDebit) return await RollbackAndFail(dbTx, "Insufficient Balance", cancellationToken);

                    newBalance = currentBalance - totalDebit;
                    transactionId = $"TXN{DateTime.UtcNow:yyMMddHHmmss}{Random.Shared.Next(100, 999)}";
                    if (await _context.TransactionDetails.AnyAsync(x => x.TxnId == transactionId, cancellationToken))
                        return await RollbackAndFail(dbTx, "Duplicate TxnId found, Please try again", cancellationToken);

                    tx = new TransactionDetail
                    {
                        UserId = user.Id.ToString(), UserName = user.Name + "-" + user.Phone,
                        WlId = user.Wlid, MdId = user.Mdid, AdId = user.Adid, TxnId = transactionId,
                        ServiceName = "DMT", OperatorName = "Money Transfer", Mobileno = model.BeneficiaryMobile,
                        OldBal = currentBalance, Amount = amount, Comm = 0, Charge = charge, Cost = totalDebit,
                        NewBal = newBalance.ToString(CultureInfo.InvariantCulture), Status = "Pending", TxnType = "Debit",
                        ApiTxnId = transactionId, ApiName = ApiCode, ApiReq = Truncate(JsonConvert.SerializeObject(model), 4000),
                        ReqDate = DateTime.Now, UpdateDate = DateTime.Now, CustomerName = model.BeneficiaryName,
                        AccountNo = model.AccountNumber, ComingFrom = model.ComingFrom, IfscCode = model.IFSC,
                        BankName = model.BankName, Tds = 0, MdComm = 0, AdComm = 0, WlComm = 0,
                        ServiceId = ServiceId, SuperAdminShare = 0
                    };
                    _context.TransactionDetails.Add(tx);
                    await _context.SaveChangesAsync(cancellationToken);
                    await dbTx.CommitAsync(cancellationToken);
                }
                catch
                {
                    await dbTx.RollbackAsync(CancellationToken.None);
                    throw;
                }
            }

            try
            {
                var (_, _, debitId) = await _walletService.DebitAsync(user.Id, user.Name + "-" + user.Phone,
                    amount, totalDebit, charge, 0, "Money_Transfer_Debit",
                    $"DMT Payment For Account No {tx.AccountNo}| Debit by Services | Amount Debit For DMT TxnId {tx.TxnId}",
                    user.Wlid, CancellationToken.None);
                if (debitId <= 0 || !await _context.Tbluserbalances.AnyAsync(x => x.Id == debitId && x.UserId == user.Id, CancellationToken.None))
                {
                    tx.Status = "FAILED"; tx.ApiMsg = "Wallet debit failed before API call"; tx.UpdateDate = DateTime.Now;
                    await _context.SaveChangesAsync(CancellationToken.None);
                    return Fail("Debit entry not found in tbluserbalance after insert");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "RBL wallet debit failed for {TransactionId}", tx.TxnId);
                tx.Status = "FAILED"; tx.ApiMsg = "Wallet debit failed before API call"; tx.UpdateDate = DateTime.Now;
                await _context.SaveChangesAsync(CancellationToken.None);
                return Fail("Please try again later, there is an issue with your wallet");
            }

            var apiResponse = await CallRblApi(model, transactionId, cancellationToken);
            var header = apiResponse?.Payment?.Header;
            var body = apiResponse?.Payment?.Body;
            var success = string.Equals(header?.Status, "Success", StringComparison.OrdinalIgnoreCase) && header?.Resp_cde == "00";
            var definitiveFailure = apiResponse?.Payment != null && !success;
            var status = success ? "SUCCESS" : definitiveFailure ? "FAILED" : "PENDING";
            var providerReference = body?.RRN ?? body?.channelpartnerrefno ?? body?.RefNo ?? string.Empty;

            tx.Status = status;
            tx.ApiTxnId = body?.RefNo ?? transactionId;
            tx.Brid = providerReference;
            tx.ApiRes = Truncate(apiResponse == null ? "API transport/parse error" : JsonConvert.SerializeObject(apiResponse), 4000);
            tx.ApiMsg = success ? "Success" : header?.Error_Desc ?? "RBL API call outcome unknown";
            tx.UpdateDate = DateTime.Now;
            await _context.SaveChangesAsync(CancellationToken.None);

            if (success)
            {
                await _commissionService.DistributeCommissionAsync(tx, user, amount, planId, ServiceId, ApiCode,
                    $"Commission Credit DMT Payment For Account No {tx.AccountNo}| Credit by Services");
                await _context.SaveChangesAsync(CancellationToken.None);
                _ = _smsService.SendTransactionSmsAsync(model.BeneficiaryMobile, model.AccountNumber, amount.ToString(CultureInfo.InvariantCulture), "65c4a796d6fc051e5652e402");
            }
            else if (definitiveFailure)
            {
                await RefundAsync(tx, user);
            }

            return Result(model, transactionId, providerReference, status, charge, newBalance, header?.Error_Desc);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return Fail("Request cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled error during RBL money transfer");
            return Fail("ERR:500 Unable to process transaction");
        }
    }

    private async Task<RblPaymentResponse?> CallRblApi(AeronpayDmtRequest model, string transactionId, CancellationToken ct)
    {
        var payload = new { Single_Payment_Corp_Req = new {
            Header = new { TranID = transactionId, Corp_ID = _config.CorpId, Maker_ID = _config.MakerId, Checker_ID = _config.CheckerId, Approver_ID = _config.ApproverId },
            Body = new { Amount = model.Amount!.Value.ToString("0.##", CultureInfo.InvariantCulture), Debit_Acct_No = _config.DebitAccountNumber,
                Debit_Acct_Name = _config.DebitAccountName, Debit_IFSC = _config.DebitIfsc, Debit_Mobile = _config.DebitMobile,
                Debit_TrnParticulars = "Settlement Payment", Debit_PartTrnRmks = "Settlement Payout", Ben_IFSC = model.IFSC,
                Ben_Acct_No = model.AccountNumber, Ben_Name = model.BeneficiaryName, Ben_Address = "India", Ben_BankName = model.BankName,
                Ben_BankCd = "0", Ben_BranchCd = "0", Ben_Email = "", Ben_Mobile = model.BeneficiaryMobile,
                Ben_TrnParticulars = "Settlement Transfer", Ben_PartTrnRmks = "Received", Issue_BranchCd = "0000",
                Mode_of_Pay = "IMPS", Remarks = string.IsNullOrWhiteSpace(model.Remark) ? "DMR" : model.Remark, RptCode = "HSBA" },
            Signature = new { Signature = "Settlement Txn" }
        }};
        var json = JsonConvert.SerializeObject(payload);
        try
        {
            var uri = new UriBuilder(_config.PaymentUrl) { Query = $"client_id={Uri.EscapeDataString(_config.ClientId)}&client_secret={Uri.EscapeDataString(_config.ClientSecret)}" }.Uri;
            using var request = new HttpRequestMessage(HttpMethod.Post, uri);
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_config.Username}:{_config.Password}")));
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");
            var httpClient = _httpClientFactory.CreateClient("RBL");
            using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
            var responseText = await response.Content.ReadAsStringAsync(ct);
            _context.Apilogs.Add(new Apilog { Apiname = "RBL-Transfer", Reqdatae = DateTime.Now, Request = Truncate(json, 4000), Response = Truncate(responseText, 4000) });
            await _context.SaveChangesAsync(CancellationToken.None);
            if (!response.IsSuccessStatusCode) { _logger.LogWarning("RBL returned HTTP {StatusCode} for {TransactionId}", (int)response.StatusCode, transactionId); return null; }
            return JsonConvert.DeserializeObject<RblPaymentResponse>(responseText);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            _logger.LogError(ex, "RBL API transport/parse failure for {TransactionId}", transactionId);
            return null;
        }
    }

    private async Task RefundAsync(TransactionDetail tx, TblUser user)
    {
        var charge = tx.Charge ?? 0; var total = (tx.Amount ?? 0) + charge;
        var (_, _, creditId) = await _walletService.CreditAsync(user.Id, user.Username + "-" + user.Phone,
            tx.Amount ?? 0, total, charge, 0, "Money_Transfer_Refund",
            $"Money Transfer Refunded | DMT Payment For Account No {tx.AccountNo}| Credit by Services | Refund Credit TXN:{tx.TxnId}", user.Wlid);
        if (creditId <= 0 || !await _context.Tbluserbalances.AnyAsync(x => x.Id == creditId && x.UserId == user.Id))
        { tx.Status = "PENDING"; tx.ApiMsg = "Refund credit not verified — kept as PENDING for retry"; await _context.SaveChangesAsync(); }
    }

    private static bool Validate(AeronpayDmtRequest model, out string message)
    {
        message = model.Amount is < 100 or > 100000 ? "Amount should be in range of 100-100000" :
            string.IsNullOrWhiteSpace(model.AccountNumber) ? "Account number is required" :
            string.IsNullOrWhiteSpace(model.BeneficiaryName) ? "Beneficiary name is required" :
            string.IsNullOrWhiteSpace(model.BeneficiaryMobile) ? "Beneficiary mobile is required" :
            string.IsNullOrWhiteSpace(model.IFSC) ? "IFSC is required" : string.Empty;
        return message.Length == 0;
    }

    private static async Task<LoginModel> RollbackAndFail(Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction tx, string message, CancellationToken ct)
    { await tx.RollbackAsync(ct); return Fail(message); }
    private static string Truncate(string? value, int length) => string.IsNullOrEmpty(value) ? string.Empty : value.Length <= length ? value : value[..length];
    private static LoginModel Fail(string message) => new() { Status_Code = "0", Message = message, Data = null };
    private static LoginModel Result(AeronpayDmtRequest model, string txnId, string reference, string status, decimal charge, decimal balance, string? error) => new()
    {
        Status_Code = status is "SUCCESS" or "PENDING" ? "1" : "0",
        Message = status is "SUCCESS" or "PENDING" ? "Transaction Successful" : "Transaction Failed || " + error,
        Data = new List<DMTTXN> { new() { AccountNo = model.AccountNumber, BeneName = model.BeneficiaryName,
            Amount = model.Amount!.Value.ToString(CultureInfo.InvariantCulture), Charge = charge.ToString("0.00"),
            CurrentBalance = balance.ToString("0.00"), Status = status, TxnID = txnId, BR_Id = reference,
            TxnDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") } }
    };
}

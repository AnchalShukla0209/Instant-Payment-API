using InstantPay.Application.Interfaces;
using InstantPay.Infrastructure.Sql.Entities;
using InstantPay.SharedKernel.RequestPayload;
using InstantPay.SharedKernel.Results.MoneyTransfer.Castler;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json.Linq;
using System.Text;

namespace InstantPay.Application.Services
{
    public class AccountVerifyService : IAccountVerifyService
    {
        private readonly AppDbContext _context;
        private readonly IWalletService _walletService;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;

        private const decimal VerificationCharge = 4m;

        public AccountVerifyService(
            AppDbContext context,
            IWalletService walletService,
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration)
        {
            _context = context;
            _walletService = walletService;
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
        }

        public async Task<LoginModel> VerifyAccountAsync(AccountVerifyRequest request, CancellationToken cancellationToken = default)
        {
            try
            {
                if (!int.TryParse(request.UserId, out int userId))
                    return Fail("Invalid UserId");

                var user = await _context.TblUsers
                    .FirstOrDefaultAsync(x => x.Id == userId && x.Status == "Active", cancellationToken);

                if (user == null)
                    return Fail("User not found or inactive");

                decimal currentBalance = await _walletService.GetBalanceAsync(userId, cancellationToken);

                if (currentBalance < VerificationCharge)
                    return Fail("Insufficient Balance");

                string refId = "DMT" + DateTime.Now.ToString("yyyyMMddHHmmssfff");
                decimal newBalance = currentBalance - VerificationCharge;

                var tx = new TransactionDetail
                {
                    UserId       = user.Id.ToString(),
                    UserName     = user.Name + "-" + user.Phone,
                    WlId         = user.Wlid,
                    MdId         = user.Mdid,
                    AdId         = user.Adid,
                    TxnId        = refId,
                    ServiceName  = "DMT",
                    OperatorName = "AccountVarify",
                    OpId         = null,
                    Mobileno     = request.SenderMobile,
                    OldBal       = currentBalance,
                    Amount       = VerificationCharge,
                    Comm         = 0,
                    Charge       = 0,
                    Cost         = VerificationCharge,
                    NewBal       = newBalance.ToString("0.00"),
                    Status       = "Pending",
                    Brid         = null,
                    TxnType      = "Debit",
                    ApiTxnId     = refId,
                    ApiName      = "QuickEKYC",
                    AdminRemarks = null,
                    ApiMsg       = null,
                    ApiRes       = null,
                    ApiReq       = null,
                    ReqDate      = DateTime.Now,
                    UpdateDate   = DateTime.Now,
                    CustomerName = request.BeneName,
                    AccountNo    = request.AccountNo,
                    ComingFrom   = "Web",
                    IfscCode     = request.IfscCode,
                    BankName     = request.BankName,
                    Tds          = 0,
                    TxnMode      = "IMPS",
                    MdComm       = 0,
                    AdComm       = 0,
                    WlComm       = 0,
                    ServiceId    = 6
                };

                await _context.TransactionDetails.AddAsync(tx, cancellationToken);
                await _context.SaveChangesAsync(cancellationToken);

                // ── Debit wallet with verification ────────────────────────────
                try
                {
                    var (_, actualNewBalance, debitEntryId) = await _walletService.DebitAsync(
                        user.Id, user.Name + "-" + user.Phone,
                        VerificationCharge, VerificationCharge,
                        0m, 0m,
                        "AccountVerify_Debit",
                        $"Account Verification Charge for AccountNo: {request.AccountNo} TXN:{refId}",
                        user.Wlid, CancellationToken.None);

                    bool debitVerified = debitEntryId > 0
                        && await _context.Tbluserbalances.AnyAsync(
                               b => b.Id == debitEntryId && b.UserId == user.Id, CancellationToken.None);

                    if (!debitVerified)
                    {
                        tx.Status     = "FAILED";
                        tx.ApiMsg     = "Wallet debit failed before API call";
                        tx.UpdateDate = DateTime.Now;
                        try { await _context.SaveChangesAsync(CancellationToken.None); } catch { }
                        return Fail("Debit entry not found in tbluserbalance after insert");
                    }

                    newBalance = actualNewBalance;
                }
                catch
                {
                    tx.Status     = "FAILED";
                    tx.ApiMsg     = "Wallet debit failed before API call";
                    tx.UpdateDate = DateTime.Now;
                    try { await _context.SaveChangesAsync(CancellationToken.None); } catch { }
                    return Fail("Please try again later, there is an issue with your wallet");
                }
                // ─────────────────────────────────────────────────────────────

                string apiKey = _configuration["QuickEKYC:ApiKey"] ?? "6432d9ea-0532-4fe3-9d4e-6ad30e3d41a9";
                string apiUrl = _configuration["QuickEKYC:BankVerifyUrl"] ?? "https://api.quickekyc.com/api/v1/bank-verification";

                string requestBody = $"{{\"key\": \"{apiKey}\",\"id_number\": \"{request.AccountNo}\",\"ifsc\": \"{request.IfscCode}\"}}";
                tx.ApiReq = requestBody;

                var (apiSuccess, apiResponseJson) = await CallQuickEkycAsync(apiUrl, requestBody, cancellationToken);

                _context.Apilogs.Add(new Apilog
                {
                    Apiname  = "verify-account-number",
                    Reqdatae = DateTime.Now,
                    Request  = requestBody,
                    Response = apiResponseJson
                });

                if (apiSuccess)
                {
                    JObject jObj = JObject.Parse(apiResponseJson);
                    string status = jObj["status"]?.ToString() ?? "";

                    if (status.ToLower() == "success")
                    {
                        string fullName    = jObj["data"]?["full_name"]?.ToString() ?? request.BeneName ?? "";
                        string txnId       = jObj["request_id"]?.ToString() ?? refId;
                        string message     = jObj["message"]?.ToString() ?? "Transaction Successful.";

                        tx.Status       = "SUCCESS";
                        tx.CustomerName = fullName;
                        tx.ApiTxnId     = txnId;
                        tx.Brid         = txnId;
                        tx.ApiRes       = apiResponseJson;
                        tx.ApiMsg       = message;
                        tx.UpdateDate   = DateTime.Now;

                        await _context.SaveChangesAsync(cancellationToken);

                        return new LoginModel
                        {
                            Status_Code = "1",
                            Message     = "Transaction Successful.",
                            Data        = new List<DMTTXN>
                            {
                                new DMTTXN
                                {
                                    AccountNo      = request.AccountNo,
                                    BeneName       = fullName,
                                    Amount         = VerificationCharge.ToString("0.00"),
                                    Charge         = "0.00",
                                    CurrentBalance = newBalance.ToString("0.00"),
                                    Status         = "Success",
                                    TxnID          = txnId,
                                    BR_Id          = txnId,
                                    TxnDate        = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")
                                }
                            }
                        };
                    }
                    else
                    {
                        string message = jObj["message"]?.ToString() ?? "Verification Failed";

                        tx.Status     = "FAILED";
                        tx.ApiRes     = apiResponseJson;
                        tx.ApiMsg     = message;
                        tx.UpdateDate = DateTime.Now;
                        await _context.SaveChangesAsync(CancellationToken.None);

                        await RefundAsync(tx, user);

                        return new LoginModel { Status_Code = "0", Message = message, Data = message };
                    }
                }
                else
                {
                    tx.Status     = "FAILED";
                    tx.ApiRes     = apiResponseJson;
                    tx.ApiMsg     = "API call failed";
                    tx.UpdateDate = DateTime.Now;
                    await _context.SaveChangesAsync(CancellationToken.None);

                    await RefundAsync(tx, user);

                    return Fail("Account verification API call failed");
                }
            }
            catch (Exception ex)
            {
                return Fail("ERR:500 " + ex.Message);
            }
        }

        private async Task RefundAsync(TransactionDetail tx, TblUser user)
        {
            var (_, _, creditEntryId) = await _walletService.CreditAsync(
                user.Id, user.Name + "-" + user.Phone,
                VerificationCharge, VerificationCharge,
                0m, 0m,
                "AccountVerify_Refund",
                $"Refund For Account Verification TXN:{tx.TxnId}",
                user.Wlid);

            bool creditVerified = creditEntryId > 0
                && await _context.Tbluserbalances.AnyAsync(
                       b => b.Id == creditEntryId && b.UserId == user.Id);

            if (!creditVerified)
            {
                tx.Status = "PENDING";
                tx.ApiMsg = "Refund credit not verified — kept as PENDING for retry";
                tx.UpdateDate = DateTime.Now;
                try { await _context.SaveChangesAsync(CancellationToken.None); } catch { }
            }
        }

        private async Task<(bool Success, string ResponseJson)> CallQuickEkycAsync(
            string url, string requestBody, CancellationToken cancellationToken)
        {
            try
            {
                var client = _httpClientFactory.CreateClient();
                var content = new StringContent(requestBody, Encoding.UTF8, "application/json");
                var response = await client.PostAsync(url, content, cancellationToken);
                string json = await response.Content.ReadAsStringAsync(cancellationToken);
                return (true, json);
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        private static LoginModel Fail(string msg) =>
            new LoginModel { Status_Code = "0", Message = msg, Data = null };
    }
}

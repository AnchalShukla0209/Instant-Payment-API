using InstantPay.Application.Interfaces;
using InstantPay.Application.Interfaces.MoneyTransfer.RechargeKit;
using InstantPay.Application.Interfaces.SMS;
using InstantPay.Infrastructure.Sql.Entities;
using InstantPay.SharedKernel.Entity.RechargeKitConfigDTO;
using InstantPay.SharedKernel.RequestPayload.MoneyTransfer.AeronPay;
using InstantPay.SharedKernel.RequestPayload.MoneyTransfer.RechargeKit;
using InstantPay.SharedKernel.Results.MoneyTransfer.Castler;
using InstantPay.SharedKernel.Results.MoneyTransfer.RechargeKit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace InstantPay.Application.Services.MoneyTransfer
{
    public class RechargeKitDmtService : IRechargeKitDmtService
    {
        private readonly RechargeKitConfig _config;
        private readonly AppDbContext _context;
        private readonly IWalletService _walletService;
        private readonly ICommissionService _commissionService;
        private readonly ISmsService _smsService;

        private const int ServiceId = 6;
        private const string ApiCode = "RKIT";

        private static readonly Dictionary<string, int> _creditCardProviderIds = new(StringComparer.OrdinalIgnoreCase)
        {
            ["SBI Credit Card"] = 617,
            ["Kotak Mahindra Bank Credit Card"] = 633,
            ["IndusInd Credit Card"] = 631,
            ["IDBI Bank Credit Card"] = 627,
            ["Federal Bank Credit Card"] = 625,
            ["Canara Credit Card"] = 622,
            ["BoB Credit Card"] = 415,
            ["AU Bank Credit Card"] = 618,
            ["Axis Bank Credit Card"] = 619,
            ["Bank of Maharashtra Credit Card"] = 620,
            ["DBS Bank Credit Card"] = 623,
            ["Dhanlaxmi Bank Limited"] = 624,
            ["HDFC Credit Card"] = 626,
            ["HSBC Credit Card"] = 639,
            ["ICICI Credit card"] = 621,
            ["IDFC FIRST Bank Credit Card"] = 628,
            ["One - Indian Bank Credit Card"] = 629,
            ["Indian bank credit card"] = 630,
            ["IOB Credit Card"] = 632,
            ["One - BOBCARD Credit Card"] = 634,
            ["Punjab National Bank Credit Card"] = 635,
            ["RBL Bank Credit Card"] = 938,
            ["Saraswat Co-Operative Bank Ltd"] = 640,
            ["One - South Indian Bank Credit Card"] = 637,
            ["Union Bank of India Credit Card"] = 641,
            ["Yes Bank Credit Card"] = 638,
            ["SIB One Credit Card (South Indian Bank)"] = 617
        };

        public RechargeKitDmtService(IOptions<RechargeKitConfig> config, AppDbContext context, IWalletService walletService, ICommissionService commissionService, ISmsService smsService)
        {
            _config = config.Value;
            _context = context;
            _walletService = walletService;
            _commissionService = commissionService;
            _smsService = smsService;
        }

        private static string Truncate(string input, int maxLen)
        {
            if (string.IsNullOrEmpty(input)) return string.Empty;
            return input.Length <= maxLen ? input : input.Substring(0, maxLen);
        }

        public bool VerifyPin(string inputPin, string txnPin)
        {
            return inputPin.ToLower().Trim() == txnPin.ToLower().Trim();
        }

        // status: 1=SUCCESS (only when optransid not empty), 2=PENDING, 3=FAILURE, others=PENDING(hold)
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

        private static string MapStatusCheckStatus(int status, string optransid)
        {
            return status switch
            {
                1 => string.IsNullOrWhiteSpace(optransid) ? "PENDING" : "SUCCESS",
                2 => "PENDING",
                3 => "FAILED",
                _ => "PENDING"
            };
        }

        private Task<decimal> GetCommissionFromPlanAsync(int planId, decimal amount, string shareColumn)
            => _commissionService.GetCommissionFromPlanAsync(planId, amount, ServiceId, ApiCode, shareColumn);

        private Task DistributeCommissionAsync(TransactionDetail tx, TblUser user, decimal amount, int planId)
            => _commissionService.DistributeCommissionAsync(
                tx, user, amount, planId, ServiceId, ApiCode,
                $"Commission Credit DMT Payment For Account No {tx.AccountNo}| Credit by Services");

        private async Task RefundUserAsync(TransactionDetail tx)
        {
            var user = await _context.TblUsers.FirstOrDefaultAsync(x => x.Id == Convert.ToInt32(tx.UserId));
            if (user == null) return;

            decimal rtComm = Convert.ToDecimal(tx.Charge);
            decimal totalRefund = Convert.ToDecimal(tx.Amount) + rtComm;

            var (_, _, creditEntryId) = await _walletService.CreditAsync(
                user.Id, user.Username + "-" + user.Phone,
                tx.Amount ?? 0, totalRefund, rtComm, 0,
                "Money_Transfer_Refund",
                $"Money Transfer Refunded | DMT Payment For Account No {tx.AccountNo}| Credit by Services | Refund Credit TXN:{tx.TxnId}",
                user.Wlid);

            bool creditVerified = creditEntryId > 0
                && await _context.Tbluserbalances.AnyAsync(
                       b => b.Id == creditEntryId && b.UserId == user.Id);

            if (!creditVerified)
            {
                tx.Status = "PENDING";
                tx.ApiMsg = "Refund credit not verified — kept as PENDING for retry";
                tx.UpdateDate = DateTime.Now;
                try { await _context.SaveChangesAsync(); } catch { }
            }
        }

        public async Task<LoginModel> MoneyTransfer(AeronpayDmtRequest model, string ip, CancellationToken cancellationToken)
        {
            try
            {
                var user = await _context.TblUsers.FirstOrDefaultAsync(x => x.Id == Convert.ToInt32(model.UserId), cancellationToken);
                if (user == null)
                    return Fail("User not found");

                if (!VerifyPin(model.TransactionPin, user.TxnPin))
                    return Fail("Invalid Pin");

                TransactionDetail tx;
                decimal currentBalance;
                decimal rtComm;
                decimal totalDebit;
                decimal newBal;
                string clientReferenceId;
                int planId;

                await using var dbTx = await _context.Database.BeginTransactionAsync(cancellationToken);
                try
                {
                    string appLockName = $"DMT_{user.Id}_{model.AccountNumber}_{model.Amount}";
                    int lockResult = (await _context.Database
                        .SqlQueryRaw<int>(
                            "DECLARE @r INT; EXEC @r = sp_getapplock @Resource = {0}, @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 5000; SELECT @r",
                            appLockName)
                        .ToListAsync()).First();

                    if (lockResult < 0)
                    {
                        await dbTx.RollbackAsync(cancellationToken);
                        return Fail("Transaction already in progress, please try again");
                    }

                    var duplicate = await _context.TransactionDetails.AnyAsync(
                        x => x.UserId == user.Id.ToString()
                          && x.Amount == model.Amount
                          && x.AccountNo == model.AccountNumber
                          && x.ServiceName == "DMT"
                          && x.ReqDate >= DateTime.Now.AddSeconds(-150),
                        cancellationToken);

                    if (duplicate || (model.Amount < 1000 || model.Amount > 100000))
                    {
                        await dbTx.RollbackAsync(cancellationToken);
                        return Fail(duplicate ? "Duplicate Transaction" : "Amount should be in range of 1000-100000");
                    }

                    currentBalance = await _walletService.GetBalanceAsync(user.Id, cancellationToken);

                    planId = user.CommissionPlanId ?? 1;
                    rtComm = await GetCommissionFromPlanAsync(planId, Convert.ToDecimal(model.Amount), "RT");
                    totalDebit = Convert.ToDecimal(model.Amount) + rtComm;

                    if (currentBalance < totalDebit)
                    {
                        await dbTx.RollbackAsync(cancellationToken);
                        return Fail("Insufficient Balance");
                    }

                    newBal = currentBalance - totalDebit;
                    int apiRequestId = new Random().Next(100000, 9999999);
                    clientReferenceId = "RCG" + DateTime.Now.ToString("yyyyMMddHHmmss") + apiRequestId.ToString();

                    var duplicateTxn = await _context.TransactionDetails.AnyAsync(
                       x => x.TxnId.ToLower().Trim() == clientReferenceId.ToLower().Trim(), cancellationToken);

                    if (duplicateTxn)
                    {
                        await dbTx.RollbackAsync(cancellationToken);
                        return Fail("Duplicate TxnId found, Please try again");
                    }

                    tx = new TransactionDetail
                    {
                        UserId = Convert.ToString(user.Id),
                        UserName = user.Name + "-" + user.Phone,
                        WlId = user.Wlid,
                        MdId = user.Mdid,
                        AdId = user.Adid,
                        TxnId = clientReferenceId,
                        ServiceName = "DMT",
                        OperatorName = "Money Transfer",
                        OpId = null,
                        Mobileno = model.BeneficiaryMobile,
                        OldBal = currentBalance,
                        Amount = model.Amount,
                        Comm = 0,
                        Charge = rtComm,
                        Cost = totalDebit,
                        NewBal = Convert.ToString(newBal),
                        Status = "Pending",
                        Brid = null,
                        TxnType = "Debit",
                        ApiTxnId = clientReferenceId,
                        ApiName = "RKIT",
                        AdminRemarks = null,
                        ApiMsg = null,
                        ApiRes = null,
                        ApiReq = Truncate(JsonConvert.SerializeObject(model), 4000),
                        ReqDate = DateTime.Now,
                        UpdateDate = DateTime.Now,
                        CustomerName = model.BeneficiaryName,
                        AccountNo = model.AccountNumber,
                        ComingFrom = model.ComingFrom,
                        IfscCode = model.IFSC,
                        BankName = model.BankName,
                        Tds = 0,
                        TxnMode = null,
                        MdComm = 0,
                        AdComm = 0,
                        WlComm = 0,
                        ServiceId = 6,
                        SuperAdminShare = 0
                    };

                    await _context.TransactionDetails.AddAsync(tx, cancellationToken);
                    await _context.SaveChangesAsync(cancellationToken);
                    await dbTx.CommitAsync(cancellationToken);
                }
                catch
                {
                    await dbTx.RollbackAsync(CancellationToken.None);
                    throw;
                }

                try
                {
                    var (_, _, debitEntryId) = await _walletService.DebitAsync(
                        user.Id, user.Name + "-" + user.Phone,
                        model.Amount ?? 0, totalDebit, rtComm, 0,
                        "Money_Transfer_Debit",
                        $"DMT Payment For Account No {tx.AccountNo}| Debit by Services | Amount Debit For DMT TxnId {tx.TxnId}",
                        user.Wlid, CancellationToken.None);

                    bool debitVerified = debitEntryId > 0
                        && await _context.Tbluserbalances.AnyAsync(
                               b => b.Id == debitEntryId && b.UserId == user.Id, CancellationToken.None);

                    if (!debitVerified)
                    {
                        tx.Status = "FAILED";
                        tx.ApiMsg = "Wallet debit failed before API call";
                        tx.UpdateDate = DateTime.Now;
                        try { await _context.SaveChangesAsync(CancellationToken.None); } catch
                        {
                            await _context.SaveChangesAsync(CancellationToken.None);
                                
                        }
                        return Fail("Debit entry not found in tbluserbalance after insert");
                    }
                }
                catch
                {
                    tx.Status = "FAILED";
                    tx.ApiMsg = "Wallet debit failed before API call";
                    tx.UpdateDate = DateTime.Now;
                    try { await _context.SaveChangesAsync(CancellationToken.None); } 
                    catch {
                        await _context.SaveChangesAsync(CancellationToken.None);
                    }
                    return Fail("Please try again later, there is an issue with your wallet");
                }

                var apiResponse = await CallRechargeKitPayoutApi(model, clientReferenceId, cancellationToken);

                string status = "FAILED";
                string orderid = "";
                string optransid = "";

                if (apiResponse != null && apiResponse.error == 0)
                {
                    optransid = apiResponse.optransid ?? "";
                    orderid = apiResponse.orderid ?? "";
                    status = MapPayoutStatus(apiResponse.status, optransid);

                    tx.ApiTxnId = !string.IsNullOrEmpty(orderid) ? orderid : clientReferenceId;
                    tx.ApiRes = JsonConvert.SerializeObject(apiResponse);
                    tx.ApiMsg = apiResponse.msg;
                    tx.Brid = optransid;
                    tx.UpdateDate = DateTime.Now;
                    await _context.SaveChangesAsync(cancellationToken);

                    if (status == "SUCCESS")
                    {
                        tx.Status = "SUCCESS";
                        await DistributeCommissionAsync(tx, user, Convert.ToDecimal(model.Amount), planId);
                        await _context.SaveChangesAsync(cancellationToken);

                        _ = _smsService.SendTransactionSmsAsync(model.BeneficiaryMobile, model.AccountNumber, model.Amount.ToString(), "65c4a796d6fc051e5652e402");
                    }
                    else if (status == "PENDING")
                    {
                        tx.Status = "PENDING";
                        await _context.SaveChangesAsync(cancellationToken);

                        _ = _smsService.SendTransactionSmsAsync(model.BeneficiaryMobile, model.AccountNumber, model.Amount.ToString(), "65c4a796d6fc051e5652e402");
                    }
                    else
                    {
                        tx.Status = "FAILED";
                        await _context.SaveChangesAsync(CancellationToken.None);
                        await RefundUserAsync(tx);
                    }
                }
                else
                {
                    tx.Status = "PENDING";
                    tx.ApiRes = apiResponse != null ? JsonConvert.SerializeObject(apiResponse) : "API Timeout Error";
                    tx.ApiMsg = apiResponse?.msg ?? "API call failed";
                    tx.UpdateDate = DateTime.Now;
                    await _context.SaveChangesAsync(CancellationToken.None);
                    //await RefundUserAsync(tx);
                }

                var list = new List<DMTTXN>
                {
                    new DMTTXN
                    {
                        AccountNo = model.AccountNumber,
                        BeneName = model.BeneficiaryName,
                        Amount = model.Amount.ToString(),
                        Charge = rtComm.ToString("0.00"),
                        CurrentBalance = newBal.ToString("0.00"),
                        Status = status,
                        TxnID = clientReferenceId,
                        BR_Id = optransid,
                        TxnDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")
                    }
                };

                return new LoginModel
                {
                    Status_Code = status == "SUCCESS" || status == "PENDING" ? "1" : "0",
                    Message = status == "SUCCESS" || status == "PENDING" ? "Transaction Successful" : "Transaction Failed || " + (apiResponse?.msg ?? ""),
                    Data = list
                };
            }
            catch (Exception ex)
            {
                return Fail("ERR:500 " + ex.Message);
            }
        }

        private async Task<RechargeKitPayoutApiResponse> CallRechargeKitPayoutApi(AeronpayDmtRequest model, string partnerRequestId, CancellationToken cancellationToken)
        {
            try
            {
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

                var bodyObj = new
                {
                    mobile_no = "8684020633",
                    account_no = model.AccountNumber,
                    ifsc = model.IFSC,
                    bank_name = model.BankName,
                    beneficiary_name = model.BeneficiaryName,
                    amount = model.Amount,
                    transfer_type = _config.TransferType ?? "5",
                    partner_request_id = partnerRequestId
                };

                string json = JsonConvert.SerializeObject(bodyObj);
                byte[] jsonBytes = Encoding.UTF8.GetBytes(json);

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
                    },
                };

                using var client = new HttpClient(handler)
                {
                    Timeout = TimeSpan.FromMinutes(5)
                };

                var request = new HttpRequestMessage(HttpMethod.Post, _config.PayoutUrl);
                request.Content = new ByteArrayContent(jsonBytes);
                request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
                request.Headers.Add("Authorization", $"Bearer {_config.BearerToken}");
                var response = await client.SendAsync(request, cancellationToken);
                string resp = await response.Content.ReadAsStringAsync(cancellationToken);

                var log = new Apilog
                {
                    Apiname = "Transaction-RKIT",
                    Reqdatae = DateTime.Now,
                    Request = json,
                    Response = resp
                };
                _context.Apilogs.Add(log);
                await _context.SaveChangesAsync(cancellationToken);

                return JsonConvert.DeserializeObject<RechargeKitPayoutApiResponse>(resp);
            }
            catch(Exception ex)
            {
                var log = new Apilog
                {
                    Apiname = "Transaction-RKIT-ERROR",
                    Reqdatae = DateTime.Now,
                    Request = JsonConvert.SerializeObject(model),
                    Response = ex.ToString()
                };

                _context.Apilogs.Add(log);
                await _context.SaveChangesAsync();

                return null;
            }
        }

        private async Task<bool> EnsureDebitForSuccessAsync(TransactionDetail tx, TblUser user, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(tx.TxnId) || tx.Amount == null)
                return false;

            decimal rtComm = Convert.ToDecimal(tx.Charge);
            decimal totalDebit = Convert.ToDecimal(tx.Amount) + rtComm;

            if (totalDebit <= 0)
                return false;

            decimal debitSum = await _context.Tbluserbalances
                .Where(b => b.UserId == user.Id &&
                            b.TxnType == "Money_Transfer_Debit" &&
                            b.Remarks != null &&
                            b.Remarks.Contains(tx.TxnId))
                .SumAsync(b => b.Amount ?? 0m, cancellationToken);

            decimal refundSum = await _context.Tbluserbalances
                .Where(b => b.UserId == user.Id &&
                            b.TxnType == "Money_Transfer_Refund" &&
                            b.Remarks != null &&
                            b.Remarks.Contains(tx.TxnId))
                .SumAsync(b => b.Amount ?? 0m, cancellationToken);

            decimal netDebit = debitSum - refundSum;
            if (netDebit >= totalDebit)
                return true;

            decimal missingDebit = totalDebit - netDebit;
            decimal reDebitSurComm = missingDebit * rtComm / totalDebit;
            decimal reDebitTxnAmount = missingDebit - reDebitSurComm;

            var (oldBal, newBal, debitEntryId) = await _walletService.DebitAsync(
                user.Id, user.Name + "-" + user.Phone,
                reDebitTxnAmount, missingDebit, reDebitSurComm, 0,
                "Money_Transfer_Debit",
                $"DMT Payment For Account No {tx.AccountNo}| Debit by Services | Amount Debit For DMT TxnId {tx.TxnId}",
                user.Wlid, cancellationToken);

            bool debitVerified = debitEntryId > 0
                && await _context.Tbluserbalances.AnyAsync(
                       b => b.Id == debitEntryId && b.UserId == user.Id, cancellationToken);

            if (debitVerified)
            {
                tx.OldBal = oldBal;
                tx.NewBal = Convert.ToString(newBal);
            }

            return debitVerified;
        }

        private async Task<bool> EnsureCcBillDebitForSuccessAsync(TransactionDetail tx, TblUser user, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(tx.TxnId) || tx.Amount == null)
                return false;

            decimal charge = tx.Charge ?? 0;
            decimal gst = tx.Tds ?? 0;
            decimal totalDebit = tx.Cost ?? (tx.Amount.Value + charge + gst);

            if (totalDebit <= 0)
                return false;

            decimal debitSum = await _context.Tbluserbalances
                .Where(b => b.UserId == user.Id &&
                            b.TxnType == "Credit_Card_Bill_Debit" &&
                            b.Remarks != null &&
                            b.Remarks.Contains(tx.TxnId))
                .SumAsync(b => b.Amount ?? 0m, cancellationToken);

            decimal refundSum = await _context.Tbluserbalances
                .Where(b => b.UserId == user.Id &&
                            b.TxnType == "Credit_Card_Bill_Refund" &&
                            b.Remarks != null &&
                            b.Remarks.Contains(tx.TxnId))
                .SumAsync(b => b.Amount ?? 0m, cancellationToken);

            decimal netDebit = debitSum - refundSum;
            if (netDebit >= totalDebit)
                return true;

            decimal missingDebit = totalDebit - netDebit;
            decimal reDebitCharge = Math.Round(missingDebit * charge / totalDebit, 2);
            decimal reDebitGst = Math.Round(missingDebit * gst / totalDebit, 2);
            decimal reDebitTxnAmount = missingDebit - reDebitCharge - reDebitGst;

            var (oldBal, newBal, debitEntryId) = await _walletService.DebitAsync(
                user.Id, user.Name + "-" + user.Phone,
                reDebitTxnAmount, missingDebit, reDebitCharge, reDebitGst,
                "Credit_Card_Bill_Debit",
                $"Credit Card Bill Payment For Account No {tx.AccountNo}| Debit by Services | Amount Debit For CCBP TxnId {tx.TxnId}",
                user.Wlid, cancellationToken);

            bool debitVerified = debitEntryId > 0
                && await _context.Tbluserbalances.AnyAsync(
                       b => b.Id == debitEntryId && b.UserId == user.Id, cancellationToken);

            if (debitVerified)
            {
                tx.OldBal = oldBal;
                tx.NewBal = Convert.ToString(newBal);
            }

            return debitVerified;
        }

        public async Task<LoginModel> CheckStatus(string txnId, CancellationToken cancellationToken)
        {
            try
            {
                var tx = await _context.TransactionDetails
                    .FirstOrDefaultAsync(x => x.TxnId == txnId || x.ApiTxnId == txnId, cancellationToken);

                if (tx == null)
                    return Fail("Transaction not found");

                string dbStatus = tx.Status?.ToUpper() ?? "PENDING";
                if (dbStatus == "SUCCESS")
                    return Fail("Transaction already processed");

                var apiResponse = await CallRechargeKitStatusApi(tx.TxnId, cancellationToken);

                if (apiResponse == null)
                    return Fail("API Response not found");

                if (apiResponse.error != 0)
                    return Fail(apiResponse.msg ?? "Status check failed");

                string optransid = apiResponse.optransid ?? "";
                string mappedStatus = MapStatusCheckStatus(apiResponse.status, optransid);

                var user = await _context.TblUsers.FirstOrDefaultAsync(x => x.Id == Convert.ToInt32(tx.UserId), cancellationToken);
                if (user == null)
                    return Fail("User not found");

                bool isCbill = tx.ServiceId == 3;

                if (mappedStatus == "SUCCESS")
                {
                    bool debitVerified = isCbill
                        ? await EnsureCcBillDebitForSuccessAsync(tx, user, cancellationToken)
                        : await EnsureDebitForSuccessAsync(tx, user, cancellationToken);

                    if (!debitVerified)
                        return Fail($"Wallet debit not verified — TXN:{tx.TxnId} kept as {dbStatus} for retry");

                    tx.Status = "SUCCESS";
                    tx.UpdateDate = DateTime.Now;
                    if (!string.IsNullOrEmpty(optransid)) tx.Brid = optransid;
                    if (!string.IsNullOrEmpty(apiResponse.orderid)) tx.ApiTxnId = apiResponse.orderid;
                    tx.ApiMsg = apiResponse.msg;
                    await _context.SaveChangesAsync(cancellationToken);

                    if (!isCbill)
                    {
                        await DistributeCommissionAsync(tx, user, Convert.ToDecimal(tx.Amount), user.CommissionPlanId ?? 1);
                        await _context.SaveChangesAsync(cancellationToken);
                    }
                }
                else if (mappedStatus == "FAILED")
                {
                    if (dbStatus != "FAILED" && dbStatus != "REFUNDED")
                    {
                        tx.Status = "FAILED";
                        tx.UpdateDate = DateTime.Now;
                        if (!string.IsNullOrEmpty(optransid)) tx.Brid = optransid;
                        if (!string.IsNullOrEmpty(apiResponse.orderid)) tx.ApiTxnId = apiResponse.orderid;
                        tx.ApiMsg = apiResponse.msg;
                        await _context.SaveChangesAsync(cancellationToken);

                        if (isCbill)
                        {
                            await RefundCcBillUserAsync(tx);
                        }
                        else
                        {
                            decimal rtComm = Convert.ToDecimal(tx.Charge);
                            decimal totalRefund = Convert.ToDecimal(tx.Amount) + rtComm;

                            var (_, _, creditEntryId) = await _walletService.CreditAsync(
                                user.Id, user.Username + "-" + user.Phone,
                                tx.Amount ?? 0, totalRefund, rtComm, 0,
                                "Money_Transfer_Refund",
                                $"Money Transfer Refunded | DMT Payment For Account No {tx.AccountNo}| Credit by Services | Refund Credit TXN:{tx.TxnId}",
                                user.Wlid, cancellationToken);

                            bool creditVerified = creditEntryId > 0
                                && await _context.Tbluserbalances.AnyAsync(
                                       b => b.Id == creditEntryId && b.UserId == user.Id, cancellationToken);

                            if (!creditVerified)
                            {
                                tx.Status = "PENDING";
                                tx.UpdateDate = DateTime.Now;
                                try { await _context.SaveChangesAsync(CancellationToken.None); } catch { }
                                return Fail($"Refund credit not verified — TXN:{tx.TxnId} kept as PENDING for retry");
                            }
                        }
                    }
                }
                else if (mappedStatus == "PENDING" && dbStatus != "FAILED" && dbStatus != "REFUNDED")
                {
                    tx.Status = "PENDING";
                    tx.UpdateDate = DateTime.Now;
                    if (!string.IsNullOrEmpty(optransid)) tx.Brid = optransid;
                    if (!string.IsNullOrEmpty(apiResponse.orderid)) tx.ApiTxnId = apiResponse.orderid;
                    tx.ApiMsg = apiResponse.msg;
                    await _context.SaveChangesAsync(cancellationToken);
                }

                return SuccessResponse(tx, optransid.Length > 0 ? optransid : (tx.Brid ?? ""), tx.Status ?? mappedStatus);
            }
            catch (Exception ex)
            {
                return Fail("ERR:500 " + ex.Message);
            }
        }

        private async Task<RechargeKitStatusCheckResponse> CallRechargeKitStatusApi(string partnerRequestId, CancellationToken cancellationToken)
        {
            try
            {
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

                string url = $"{_config.StatusCheckUrl}?partner_request_id={Uri.EscapeDataString(partnerRequestId)}";

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
                var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Add("Authorization", $"Bearer {_config.BearerToken}");

                var response = await client.SendAsync(request, cancellationToken);
                string resp = await response.Content.ReadAsStringAsync(cancellationToken);

                var log = new Apilog
                {
                    Apiname = "RKIT-StatusCheck",
                    Reqdatae = DateTime.Now,
                    Request = partnerRequestId,
                    Response = resp
                };
                _context.Apilogs.Add(log);
                await _context.SaveChangesAsync(cancellationToken);

                return JsonConvert.DeserializeObject<RechargeKitStatusCheckResponse>(resp);
            }
            catch
            {
                return null;
            }
        }

        public async Task<RechargeKitOperatorResponse> GetCreditCardOperatorsAsync(CancellationToken cancellationToken)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(_config.OperatorFetchUrl))
                    return new RechargeKitOperatorResponse { error = 1, msg = "OperatorFetchUrl not configured", status = 3 };

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
                var request = new HttpRequestMessage(HttpMethod.Get, _config.OperatorFetchUrl);
                request.Headers.Add("Authorization", $"Bearer {_config.BearerToken}");

                var response = await client.SendAsync(request, cancellationToken);
                string resp = await response.Content.ReadAsStringAsync(cancellationToken);

                var log = new Apilog
                {
                    Apiname = "RKIT-OperatorFetch",
                    Reqdatae = DateTime.Now,
                    Request = _config.OperatorFetchUrl,
                    Response = resp
                };
                _context.Apilogs.Add(log);
                await _context.SaveChangesAsync(cancellationToken);

                var result = JsonConvert.DeserializeObject<RechargeKitOperatorResponse>(resp);

                if (result?.operatorList != null)
                {
                    foreach (var op in result.operatorList)
                    {
                        var key = op.operator_name?.Trim();
                        if (!string.IsNullOrWhiteSpace(key) && _creditCardProviderIds.TryGetValue(key, out int providerId))
                        {
                            op.providerid = providerId;
                        }
                    }
                }

                return result;
            }
            catch (Exception ex)
            {
                return new RechargeKitOperatorResponse { error = 1, msg = ex.Message, status = 3 };
            }
        }

        public async Task<LoginModel> CreditCardBillPaymentAsync(CreditCardBillPaymentRequest model, string ip, CancellationToken cancellationToken)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(model.UserId) || !int.TryParse(model.UserId, out int userId))
                    return Fail("Invalid User Id");

                var user = await _context.TblUsers.FirstOrDefaultAsync(x => x.Id == userId, cancellationToken);
                if (user == null)
                    return Fail("User not found");

                if (!VerifyPin(model.TransactionPin, user.TxnPin))
                    return Fail("Invalid Pin");

                if (string.IsNullOrWhiteSpace(model.MobileNo) || model.MobileNo.Trim().Length != 10 || model.MobileNo.Any(c => !char.IsDigit(c)))
                    return Fail("Mobile number must be 10 digits");

                if (string.IsNullOrWhiteSpace(model.AccountNo))
                    return Fail("Account number is required");

                if (string.IsNullOrWhiteSpace(model.IFSC))
                    return Fail("IFSC is required");

                if (string.IsNullOrWhiteSpace(model.BankName))
                    return Fail("Bank name is required");

                if (string.IsNullOrWhiteSpace(model.BeneficiaryName))
                    return Fail("Beneficiary name is required");

                if (string.IsNullOrWhiteSpace(model.OperatorCode))
                    return Fail("Operator code is required");

                if (model.Amount == null || model.Amount <= 0)
                    return Fail("Amount should be greater than 0");

                const int ccBillServiceId = 3;
                const string serviceName = "Credit Card Bill";
                decimal amount = model.Amount.Value;

                decimal charge;
                decimal gst;
                decimal totalDebit;
                string partnerRequestId;
                TransactionDetail tx;

                await using var dbTx = await _context.Database.BeginTransactionAsync(cancellationToken);
                try
                {
                    string appLockName = $"CCBP_{user.Id}_{model.AccountNo}_{amount}";
                    int lockResult = (await _context.Database
                        .SqlQueryRaw<int>(
                            "DECLARE @r INT; EXEC @r = sp_getapplock @Resource = {0}, @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 5000; SELECT @r",
                            appLockName)
                        .ToListAsync()).First();

                    if (lockResult < 0)
                    {
                        await dbTx.RollbackAsync(cancellationToken);
                        return Fail("Transaction already in progress, please try again");
                    }

                    var duplicate = await _context.TransactionDetails.AnyAsync(
                        x => x.UserId == user.Id.ToString()
                          && x.Amount == amount
                          && x.AccountNo == model.AccountNo
                          && x.ServiceId == ccBillServiceId
                          && x.ReqDate >= DateTime.Now.AddSeconds(-150),
                        cancellationToken);

                    if (duplicate)
                    {
                        await dbTx.RollbackAsync(cancellationToken);
                        return Fail("Duplicate Transaction");
                    }

                    decimal currentBalance = await _walletService.GetBalanceAsync(user.Id, cancellationToken);

                    int planId = user.CommissionPlanId ?? 1;
                    charge = await _commissionService.GetCommissionFromPlanAsync(planId, amount, ccBillServiceId, ApiCode, "RT");
                    gst = Math.Round(charge * 0.18m, 2);
                    totalDebit = amount + charge + gst;

                    if (currentBalance < totalDebit)
                    {
                        await dbTx.RollbackAsync(cancellationToken);
                        return Fail("Insufficient Balance");
                    }

                    decimal newBal = currentBalance - totalDebit;
                    int apiRequestId = new Random().Next(100000, 9999999);
                    partnerRequestId = "RKITCCBP" + DateTime.Now.ToString("yyyyMMddHHmmss") + apiRequestId.ToString();

                    var duplicateTxn = await _context.TransactionDetails.AnyAsync(
                        x => x.TxnId.ToLower().Trim() == partnerRequestId.ToLower().Trim(), cancellationToken);

                    if (duplicateTxn)
                    {
                        await dbTx.RollbackAsync(cancellationToken);
                        return Fail("Duplicate TxnId found, Please try again");
                    }

                    tx = new TransactionDetail
                    {
                        UserId = Convert.ToString(user.Id),
                        UserName = user.Name + "-" + user.Phone,
                        WlId = user.Wlid,
                        MdId = user.Mdid,
                        AdId = user.Adid,
                        TxnId = partnerRequestId,
                        ServiceName = serviceName,
                        OperatorName = model.BankName,
                        OpId = model.OperatorCode,
                        Mobileno = model.MobileNo,
                        OldBal = currentBalance,
                        Amount = amount,
                        Comm = 0,
                        Charge = charge,
                        Tds = gst,
                        Cost = totalDebit,
                        NewBal = Convert.ToString(newBal),
                        Status = "Pending",
                        Brid = null,
                        TxnType = "Debit",
                        ApiTxnId = partnerRequestId,
                        ApiName = ApiCode,
                        AdminRemarks = null,
                        ApiMsg = null,
                        ApiRes = null,
                        ApiReq = Truncate(JsonConvert.SerializeObject(model), 4000),
                        ReqDate = DateTime.Now,
                        UpdateDate = DateTime.Now,
                        CustomerName = model.BeneficiaryName,
                        AccountNo = model.AccountNo,
                        ComingFrom = model.ComingFrom,
                        IfscCode = model.IFSC,
                        BankName = model.BankName,
                        ServiceId = ccBillServiceId,
                        SuperAdminShare = 0
                    };

                    await _context.TransactionDetails.AddAsync(tx, cancellationToken);
                    await _context.SaveChangesAsync(cancellationToken);
                    await dbTx.CommitAsync(cancellationToken);
                }
                catch
                {
                    await dbTx.RollbackAsync(CancellationToken.None);
                    throw;
                }

                try
                {
                    var (oldBal, actualNewBal, debitEntryId) = await _walletService.DebitAsync(
                        user.Id, user.Name + "-" + user.Phone,
                        amount, totalDebit, charge, gst,
                        "Credit_Card_Bill_Debit",
                        $"{serviceName} Payment For Account No {tx.AccountNo}| Debit by Services | Amount Debit For CCBP TxnId {tx.TxnId}",
                        user.Wlid, CancellationToken.None);

                    bool debitVerified = debitEntryId > 0
                        && await _context.Tbluserbalances.AnyAsync(
                               b => b.Id == debitEntryId && b.UserId == user.Id, CancellationToken.None);

                    if (!debitVerified)
                    {
                        tx.Status = "FAILED";
                        tx.ApiMsg = "Wallet debit failed before API call";
                        tx.UpdateDate = DateTime.Now;
                        try { await _context.SaveChangesAsync(CancellationToken.None); } catch { await _context.SaveChangesAsync(CancellationToken.None); }
                        return Fail("Debit entry not found in tbluserbalance after insert");
                    }

                    tx.OldBal = oldBal;
                    tx.NewBal = Convert.ToString(actualNewBal);
                    await _context.SaveChangesAsync(CancellationToken.None);
                }
                catch
                {
                    tx.Status = "FAILED";
                    tx.ApiMsg = "Wallet debit failed before API call";
                    tx.UpdateDate = DateTime.Now;
                    try { await _context.SaveChangesAsync(CancellationToken.None); } catch { await _context.SaveChangesAsync(CancellationToken.None); }
                    return Fail("Please try again later, there is an issue with your wallet");
                }

                var apiResponse = await CallRechargeKitCcBillPaymentApi(model, partnerRequestId, cancellationToken);

                string status = "FAILED";
                string orderid = "";
                string optransid = "";

                if (apiResponse != null && apiResponse.error == 0)
                {
                    optransid = apiResponse.optransid ?? "";
                    orderid = apiResponse.orderid ?? "";
                    status = MapPayoutStatus(apiResponse.status, optransid);

                    tx.ApiTxnId = !string.IsNullOrEmpty(orderid) ? orderid : partnerRequestId;
                    tx.ApiRes = JsonConvert.SerializeObject(apiResponse);
                    tx.ApiMsg = apiResponse.msg;
                    tx.Brid = optransid;
                    tx.UpdateDate = DateTime.Now;
                    await _context.SaveChangesAsync(cancellationToken);

                    if (status == "SUCCESS")
                    {
                        tx.Status = "SUCCESS";
                        await _context.SaveChangesAsync(cancellationToken);
                    }
                    else if (status == "FAILED")
                    {
                        tx.Status = "FAILED";
                        await _context.SaveChangesAsync(CancellationToken.None);
                        await RefundCcBillUserAsync(tx);
                    }
                    else
                    {
                        tx.Status = "PENDING";
                        await _context.SaveChangesAsync(cancellationToken);
                    }
                }
                else
                {
                    tx.Status = "FAILED";
                    tx.ApiRes = apiResponse != null ? JsonConvert.SerializeObject(apiResponse) : "API Error";
                    tx.ApiMsg = apiResponse?.msg ?? "API call failed";
                    tx.UpdateDate = DateTime.Now;
                    await _context.SaveChangesAsync(CancellationToken.None);
                    await RefundCcBillUserAsync(tx);
                }

                var list = new List<DMTTXN>
                {
                    new DMTTXN
                    {
                        AccountNo = model.AccountNo,
                        BeneName = model.BeneficiaryName,
                        Amount = amount.ToString("0.00"),
                        Charge = (charge + gst).ToString("0.00"),
                        CurrentBalance = tx.NewBal,
                        Status = status,
                        TxnID = partnerRequestId,
                        BR_Id = optransid,
                        TxnDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")
                    }
                };

                return new LoginModel
                {
                    Status_Code = status == "SUCCESS" || status == "PENDING" ? "1" : "0",
                    Message = status == "SUCCESS" || status == "PENDING" ? "Transaction Successful" : "Transaction Failed || " + (apiResponse?.msg ?? ""),
                    Data = list
                };
            }
            catch (Exception ex)
            {
                return Fail("ERR:500 " + ex.Message);
            }
        }

        private async Task<RechargeKitPayoutApiResponse> CallRechargeKitCcBillPaymentApi(CreditCardBillPaymentRequest model, string partnerRequestId, CancellationToken cancellationToken)
        {
            try
            {
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

                var bodyObj = new
                {
                    mobile_no = model.MobileNo,
                    account_no = model.AccountNo,
                    ifsc = model.IFSC,
                    bank_name = model.BankName,
                    beneficiary_name = model.BeneficiaryName,
                    amount = model.Amount,
                    partner_request_id = partnerRequestId,
                    operator_code = model.OperatorCode,
                    transfer_type = _config.TransferType ?? "5"
                };

                string json = JsonConvert.SerializeObject(bodyObj);
                byte[] jsonBytes = Encoding.UTF8.GetBytes(json);

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
                var request = new HttpRequestMessage(HttpMethod.Post, _config.CreditCardBillPaymentUrl);
                request.Content = new ByteArrayContent(jsonBytes);
                request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
                request.Headers.Add("Authorization", $"Bearer {_config.BearerToken}");
                var response = await client.SendAsync(request, cancellationToken);
                string resp = await response.Content.ReadAsStringAsync(cancellationToken);

                var log = new Apilog
                {
                    Apiname = "CCBP-RKIT",
                    Reqdatae = DateTime.Now,
                    Request = json,
                    Response = resp
                };
                _context.Apilogs.Add(log);
                await _context.SaveChangesAsync(cancellationToken);

                return JsonConvert.DeserializeObject<RechargeKitPayoutApiResponse>(resp);
            }
            catch
            {
                return null;
            }
        }

        private async Task RefundCcBillUserAsync(TransactionDetail tx)
        {
            var user = await _context.TblUsers.FirstOrDefaultAsync(x => x.Id == Convert.ToInt32(tx.UserId));
            if (user == null) return;

            decimal charge = tx.Charge ?? 0;
            decimal gst = tx.Tds ?? 0;
            decimal totalRefund = tx.Cost ?? ((tx.Amount ?? 0) + charge + gst);

            var (_, _, creditEntryId) = await _walletService.CreditAsync(
                user.Id, user.Name + "-" + user.Phone,
                tx.Amount ?? 0, totalRefund, charge, gst,
                "Credit_Card_Bill_Refund",
                $"Credit Card Bill Refunded | CCBP Payment For Account No {tx.AccountNo}| Credit by Services | Refund Credit TXN:{tx.TxnId}",
                user.Wlid);

            bool creditVerified = creditEntryId > 0
                && await _context.Tbluserbalances.AnyAsync(
                       b => b.Id == creditEntryId && b.UserId == user.Id);

            if (!creditVerified)
            {
                tx.Status = "PENDING";
                tx.ApiMsg = "Refund credit not verified — kept as PENDING for retry";
                tx.UpdateDate = DateTime.Now;
                try { await _context.SaveChangesAsync(); } catch { }
            }
        }

        private LoginModel Fail(string msg)
        {
            return new LoginModel
            {
                Status_Code = "0",
                Message = msg,
                Data = null
            };
        }

        private LoginModel SuccessResponse(TransactionDetail tx, string operatorRef, string status)
        {
            var list = new List<DMTTXN>
            {
                new DMTTXN
                {
                    AccountNo = tx.AccountNo,
                    BeneName = tx.CustomerName,
                    Amount = tx.Amount.ToString(),
                    Charge = tx.Charge.ToString(),
                    CurrentBalance = tx.NewBal,
                    Status = status,
                    TxnID = tx.TxnId,
                    BR_Id = operatorRef,
                    TxnDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")
                }
            };

            return new LoginModel
            {
                Status_Code = status == "SUCCESS" ? "1" : "0",
                Message = status,
                Data = list
            };
        }
    }
}

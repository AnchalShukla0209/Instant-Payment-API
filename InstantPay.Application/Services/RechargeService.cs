using InstantPay.Application.Factory;
using InstantPay.Application.IFactory;
using InstantPay.Application.Interfaces;
using InstantPay.Infrastructure.Sql.Entities;
using InstantPay.SharedKernel.Entity;
using InstantPay.SharedKernel.RandomNumberGenerator;
using InstantPay.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InstantPay.Application.Services
{
    public class RechargeService : IRechargeService
    {
        private static readonly HashSet<string> MroboticsCompanyIds =
            ["1", "2", "4", "5", "6", "7", "11", "12", "17", "24", "27", "28"];
        private readonly IConfiguration _config;
        private readonly AppDbContext _context;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IRechargeApiProviderService _provider;
        private readonly ApiTransactionRecoveryService _recoveryService;
        private readonly ILogger<RechargeService> _logger;
        private readonly IWalletService _walletService;
        private readonly ICommissionService _commissionService;

        public RechargeService(AppDbContext context, IRechargeApiProviderService provider, ApiTransactionRecoveryService recoveryService, ILogger<RechargeService> logger, IWalletService walletService, ICommissionService commissionService, IConfiguration config, IHttpClientFactory httpClientFactory)
        {
            _context = context;
            _provider = provider;
            _recoveryService = recoveryService;
            _logger = logger;
            _walletService = walletService;
            _commissionService = commissionService;
            _config = config;
            _httpClientFactory = httpClientFactory;
        }

        public async Task<ResponseSuccess> SubmitRechargeAsync(RechargeRequestDto request)
        {
            using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);

            var user = await _context.TblUsers.FindAsync(request.UserId);
            if (user == null)
            {
                return new ResponseSuccess { success = false, message = "Invalid User" };
            }
            string UserName = $"{user.Name}-{user.Phone}";

            string ServiceId = request.Type == "BLL2" ? "1" : request.Type == "DTH2" ? "2" : "3";
            var operatorDetails = await _context.Tbloperators
                .Where(x => x.ServiceId == ServiceId
                        && x.OperatorName.Trim().ToLower() == request.Operator.Trim().ToLower()
                        && x.Status.Trim() == "Active")
                .OrderByDescending(x => x.Id)
                .FirstOrDefaultAsync();

            if (operatorDetails == null)
                return new ResponseSuccess { success = false, message = "Invalid Operator" };

            if (user.TxnPin.Trim() != request.TxnPin.Trim())
            {
                return new ResponseSuccess { success = false, message = "Invalid Transaction PIN" };
            }

            decimal currentBalance = await _walletService.GetBalanceAsync(request.UserId);
            if (currentBalance < request.Amount)
            {
                return new ResponseSuccess { success = false, message = "Insufficient balance in wallet. Please add funds." };
            }
            string serviceName = request.Type == "DTH2" ? "DTH Recharge" : request.Type == "BLL2" ? "Mobile Recharge" : request.Type == "EBILL" ? "Electricity Bill" : request.Type == "INS" ? "Insurence Bill" : request.Type == "FASTAG" ? "Fast Tag Bill" : "Credit Card Bill";
            string serviceNameReq = request.Type == "DTH2" ? "DTH" : request.Type == "BLL2" ? "RECHARGE" : "BILL PAYMENT";
            string CustomerRefNo = ReferenceGenerator.GenerateCustomerRefNo();
            string APIName;
            var operatorId = operatorDetails.Id.ToString();

            if ((request.Type == "BLL2" || request.Type == "DTH2") && MroboticsCompanyIds.Contains(request.operatorCode))
            {
                APIName = "mrobotics";
            }
            else if (new[] { "16", "18", "20", "22", "12", "13", "14", "15" }.Contains(operatorId))
            {
                APIName = "ambika";
            }
            else if (new[] { "24", "25", "26", "27", "28", "29", "30", "31", "32" }.Contains(operatorId) || (request.Type == "EBILL" || request.Type == "INS" || request.Type == "FASTAG" || request.Type == "CCBP"))
            {
                APIName = "iqore";
            }
            else
            {
                APIName = "cyrusre";
            }
            var pendingResult = await ProcessRechargeTransactionAsync(
                userKey: request.UserId,
                transactionId: CustomerRefNo,
                customerNumber: request.MobileNumber,
                amount: request.Amount,
                comingFrom: request.comingFrom,
                newStatus: "Pending",
                apiName: APIName,
                serviceName: serviceNameReq,
                operatorName: request.Operator,
                opId: operatorDetails.Id.ToString(),
                customerName: "NA",
                accountNo: request.MobileNumber
            );

            if (pendingResult != "1")
            {
                await transaction.RollbackAsync();
                return new ResponseSuccess
                {
                    success = false,
                    message = pendingResult.Contains("SQL") || pendingResult.Contains("database") || pendingResult.Contains("disk") 
                        ? "API Server Down" 
                        : "Failed to process transaction." + pendingResult
                };
            }

            // CRITICAL: Commit transaction BEFORE calling API to ensure record exists
            // This guarantees that even if API succeeds and update fails, the record is in DB
            await transaction.CommitAsync();

            // Verify transaction was actually saved to DB before calling API
            var verifyTxn = await _context.TransactionDetails
                .FirstOrDefaultAsync(t => t.TxnId == CustomerRefNo && t.UserId == Convert.ToString(request.UserId));
            
            if (verifyTxn == null)
            {
                // Transaction not saved despite commit - critical system error
                _logger.LogCritical("Transaction commit succeeded but record not found for {OrderId}", CustomerRefNo);
                return new ResponseSuccess
                {
                    success = false,
                    message = "System Error: Transaction not saved"
                };
            }




            string apiResponse = string.Empty;
            try
            {
                apiResponse = await _provider.Process(
                    provider: APIName,
                    mobile: request.MobileNumber,
                    amount: request.Amount.ToString(),
                    orderId: CustomerRefNo,
                    companyId: request.operatorCode,
                    Type: request.Type,
                    Optional: request.optional,
                    Optional1: request.optional1,
                    isStv: request.IsStv
                );
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                
                // Compensation: Check if the transaction succeeded in provider API despite local failure
                _logger.LogWarning(ex, "Recharge API call failed locally for {OrderId} via {Provider}. Checking provider status for compensation.", CustomerRefNo, APIName);
                var (status, apiTxnIdComp) = await _recoveryService.CheckTransactionStatus(APIName, CustomerRefNo);
                
                if (status == "SUCCESS")
                {
                    _logger.LogError("CRITICAL: Transaction {OrderId} succeeded in {Provider} but failed locally. Manual reconciliation required. ApiTxnId: {ApiTxnId}", 
                        CustomerRefNo, APIName, apiTxnIdComp);
                    
                    return new ResponseSuccess
                    {
                        success = false,
                        message = $"Transaction succeeded in {APIName} but failed locally. Please contact support with Order ID: {CustomerRefNo}. ApiTxnId: {apiTxnIdComp}"
                    };
                }
                
                return new ResponseSuccess
                {
                    success = false,
                    message = $"Recharge API call failed: {ex.Message}"
                };
            }

            string finalStatus = "FAILED";
            string apiTxnId = "";
            string customerRefNo = CustomerRefNo;
            string rechargeStatus = "";

            try
            {
                switch (APIName.ToLower())
                {
                    case "iqore":
                        var parts = apiResponse.Split('|');
                        if (parts.Length >= 5)
                        {
                            var code = parts[0].Trim();
                            rechargeStatus = parts[1].Trim();
                            customerRefNo = parts[2].Trim();
                            apiTxnId = parts[4].Trim();

                            finalStatus = code switch
                            {
                                "200" => "SUCCESS",
                                "201" => "PENDING",
                                "202" => "FAILED",
                                "403" => "FAILED",
                                "404" => "FAILED",
                                "503" => "FAILED",
                                _ => "PENDING"
                            };
                        }
                        else
                        {
                            throw new Exception("Invalid response format from iCore API");
                        }
                        break;

                    case "mrobotics":
                        var mObj = JObject.Parse(apiResponse);
                        var mStatus = mObj["status"]?.ToString()?.ToLowerInvariant();
                        var mHasError = mObj["error"]?.Value<bool>() == true;
                        finalStatus = (mStatus, mHasError) switch
                        {
                            ("success", false) => "SUCCESS",
                            ("failed", _) => "FAILED",
                            ("failure", _) => "FAILED",
                            (_, true) => "FAILED",
                            _ => "PENDING"
                        };
                        apiTxnId = mObj["tnx_id"]?.ToString() ?? mObj["id"]?.ToString() ?? "";
                        rechargeStatus = mObj["response"]?.ToString()
                            ?? mObj["errorMessage"]?.ToString()
                            ?? "";
                        break;

                    case "ambika":
                        if (string.IsNullOrWhiteSpace(apiResponse) || !apiResponse.TrimStart().StartsWith("{"))
                        {
                            finalStatus = "PENDING";
                            rechargeStatus = apiResponse;
                            break;
                        }
                        var aObj = JObject.Parse(apiResponse);
                        var aStatus = aObj["status"]?.ToString() ?? "";
                        finalStatus = (aStatus == "1" || aStatus == "2") ? "SUCCESS" : "FAILED";
                        apiTxnId = aObj["rpid"]?.ToString() ?? "";
                        rechargeStatus = aObj["msg"]?.ToString() ?? "";
                        break;

                    case "cyrusre":
                        var cObj = JObject.Parse(apiResponse.Split('#')[1]);
                        var cStatus = cObj["Status"].ToString().ToLower();
                        finalStatus = (cStatus == "success") ? "SUCCESS" : "FAILED";
                        apiTxnId = cObj["ApiTransID"]?.ToString() ?? "";
                        rechargeStatus = cObj["ErrorMessage"]?.ToString() ?? "";
                        break;
                }
            }
            catch (Exception ex)
            {
                // Transaction already committed, so cannot rollback
                // Record exists as PENDING - background reconciliation will fix this
                _logger.LogError(ex, "API response parsing failed for {OrderId} via {Provider}. Record saved as PENDING for reconciliation.", CustomerRefNo, APIName);
                
                // Check if transaction succeeded in provider despite parsing failure
                var (status, apiTxnIdComp) = await _recoveryService.CheckTransactionStatus(APIName, CustomerRefNo);
                
                if (status == "SUCCESS")
                {
                    _logger.LogWarning("Transaction {OrderId} succeeded in {Provider} despite parsing failure. Background reconciliation will update it. ApiTxnId: {ApiTxnId}", 
                        CustomerRefNo, APIName, apiTxnIdComp);
                    
                    // Try to update immediately in a new transaction
                    try
                    {
                        using var updateTransaction = await _context.Database.BeginTransactionAsync();
                        var immediateUpdate = await ProcessRechargeTransactionAsync(
                            userKey: request.UserId,
                            transactionId: CustomerRefNo,
                            customerNumber: request.MobileNumber,
                            amount: request.Amount,
                            comingFrom: request.comingFrom,
                            newStatus: "SUCCESS",
                            apiName: APIName,
                            serviceName: serviceNameReq,
                            operatorName: request.Operator,
                            opId: operatorDetails.Id.ToString(),
                            customerName: "NA",
                            accountNo: request.MobileNumber,
                            apiMsg: apiTxnIdComp,
                            apiResponse: "Recovered from parsing failure",
                            flagforUpdate: true
                        );
                        await updateTransaction.CommitAsync();
                    }
                    catch (Exception updateEx)
                    {
                        _logger.LogError(updateEx, "Immediate update failed for {OrderId}. Background reconciliation will handle it.", CustomerRefNo);
                    }
                }
                
                return new ResponseSuccess
                {
                    success = false,
                    message = $"API Response parsing failed: {ex.Message}"
                };
            }

            // Update transaction in a new transaction scope
            string updateResult;
            using (var updateTransaction = await _context.Database.BeginTransactionAsync())
            {
                updateResult = await ProcessRechargeTransactionAsync(
                    userKey: request.UserId,
                    transactionId: CustomerRefNo,
                    customerNumber: request.MobileNumber,
                    amount: request.Amount,
                    comingFrom: request.comingFrom,
                    newStatus: finalStatus,
                    apiName: APIName,
                    serviceName: serviceNameReq,
                    operatorName: request.Operator,
                    opId: operatorDetails.Id.ToString(),
                    customerName: "NA",
                    accountNo: request.MobileNumber,
                    apiMsg: apiTxnId,
                    apiResponse: apiResponse,
                    flagforUpdate: true
                );

                if (updateResult != "1")
                {
                    await updateTransaction.RollbackAsync();
                    
                    // Record still exists as PENDING - background reconciliation will fix this
                    if (finalStatus == "SUCCESS")
                    {
                        _logger.LogWarning("Transaction update failed for {OrderId} via {Provider}. Record saved as PENDING for reconciliation.", CustomerRefNo, APIName);
                        
                        // Verify with provider and log for background reconciliation
                        var (status, apiTxnIdComp) = await _recoveryService.CheckTransactionStatus(APIName, CustomerRefNo);
                        
                        if (status == "SUCCESS")
                        {
                            _logger.LogError("CRITICAL: Transaction {OrderId} succeeded in {Provider} but update failed. Background reconciliation will fix it. ApiTxnId: {ApiTxnId}", 
                                CustomerRefNo, APIName, apiTxnIdComp);
                        }
                    }
                    
                    return new ResponseSuccess
                    {
                        success = false,
                        message = updateResult.Contains("SQL") || updateResult.Contains("database") || updateResult.Contains("disk") 
                            ? "API Server Down" 
                            : "Failed to update transaction after API response." + updateResult
                    };
                }
                
                await updateTransaction.CommitAsync();
            }

            return new ResponseSuccess
            {
                success = (finalStatus.ToUpper() == "SUCCESS" || finalStatus.ToUpper() == "PENDING") ? true : false,
                message = finalStatus.ToUpper() == "SUCCESS" || finalStatus.ToUpper() == "PENDING"
                    ? $"{serviceName} successful."
                    : $"{serviceName} {finalStatus.ToLower()}: {rechargeStatus}",
                txnid = customerRefNo,
                apitxnid = apiTxnId,
                transactiondatetime = DateTime.UtcNow.ToLocalTime().ToString("dd-MM-yyyy hh:mm:ss tt")
            };
        }

        private string GetSlabId(string opId) => opId switch
        {
            "1" => "1",
            "2" => "4",
            "10" => "7",
            "11" => "10",
            "16" => "2",
            "17" => "3",
            "18" => "5",
            "19" => "6",
            "20" => "8",
            "21" => "9",
            "22" => "11",
            "23" => "12",
            "24" => "2",
            "25" => "5",
            "26" => "8",
            "27" => "11",
            _ => "0"
        };

        /// <summary>
        /// Performs a recharge transaction (insert or update) atomically.
        /// </summary>
        public async Task<string> ProcessRechargeTransactionAsync(
            int userKey,
            string transactionId,
            string customerNumber,
            decimal amount,
            string comingFrom,
            string apiName,
            string serviceName,
            string operatorName,
            string opId,
            string customerName,
            string accountNo,
            string apiResponse = null,
            string apiReferenceId = null,
            string newStatus = null,
            string rrn = null,
            string apiMsg = null,
            string apiReq = null,
            bool flagforUpdate = false)
        {

            try
            {
                var slabId = GetSlabId(opId);
                var data = await (
                    from u in _context.TblUsers
                    where u.Id == userKey
                    select new
                    {
                        u.Id,
                        u.Name,
                        u.Phone,
                        u.Usertype,
                        u.PlanId,
                        u.CommissionPlanId,
                        u.Adid,
                        u.Mdid,
                        u.Wlid
                    }).FirstOrDefaultAsync();

                if (data == null) return "Invalid User";

                // Detect whether this is a new-commission provider (ICORE / AMBK)
                bool isNewCommission = apiName?.ToLower() == "iqore" || apiName?.ToLower() == "ambika";
                string commApiCode = apiName?.ToLower() == "iqore" ? "ICORE"
                                   : apiName?.ToLower() == "ambika" ? "AMBK"
                                   : null;
                int commServiceId = serviceName == "RECHARGE" ? 1 : serviceName == "DTH" ? 2 : 3;
                int? opIdInt = int.TryParse(opId, out int parsedOpId) ? parsedOpId : (int?)null;
                int planIdForComm = data.CommissionPlanId ?? (int.TryParse(data.PlanId, out int p) ? p : 1);

                decimal retailerComm = 0, adComm = 0, mdComm = 0, wlComm = 0;
                decimal tds = 0;
                decimal cost;

                if (isNewCommission && commApiCode != null)
                {
                    retailerComm = await _commissionService.GetCommissionFromPlanAsync(
                        planIdForComm, amount, commServiceId, commApiCode, "RT", opIdInt);
                    tds  = 0;
                    cost = amount;
                }
                else
                {
                    tds  = 0;
                    cost = amount;
                }

                decimal balBefore = await _walletService.GetBalanceAsync(userKey);
                // Wallet was already debited when the pending record was inserted;
                // do not subtract the cost again on update.
                decimal newBal = balBefore;

                var txn = await _context.TransactionDetails
                    .FirstOrDefaultAsync(t => t.TxnId == transactionId && t.AccountNo == accountNo && t.UserId == Convert.ToString(userKey));

                if (txn == null)
                {
                    // Insert new transaction
                    txn = new TransactionDetail
                    {
                        WlComm = wlComm,
                        AdComm = adComm,
                        MdComm = mdComm,
                        Tds = tds,
                        CustomerName = customerName,
                        AccountNo = accountNo,
                        ComingFrom = comingFrom,
                        UserId = Convert.ToString(userKey),
                        UserName = $"{data.Name}-{data.Phone}",
                        WlId = data.Wlid,
                        MdId = data.Mdid,
                        AdId = data.Adid,
                        TxnId = transactionId,
                        ServiceName = serviceName,
                        OperatorName = operatorName,
                        OpId = opId,
                        Mobileno = customerNumber,
                        OldBal = balBefore,
                        Amount = amount,
                        Comm = retailerComm,
                        Charge = 0,
                        Cost = cost,
                        NewBal = Convert.ToString(newBal),
                        Status = "Pending",
                        Brid = "",
                        TxnType = "Debit",
                        ApiName = apiName,
                        ServiceId = commServiceId,
                        ReqDate = DateTime.Now
                    };

                    _context.TransactionDetails.Add(txn);

                    decimal actualNew;
                    (balBefore, actualNew, _) = await _walletService.DebitAsync(
                        userKey, $"{data.Name}-{data.Phone}",
                        amount, amount, retailerComm, tds,
                        serviceName,
                        $"Debit For {serviceName} {accountNo}",
                        data.Wlid);
                    txn.OldBal = balBefore;
                    txn.NewBal = Convert.ToString(actualNew);

                }
                else
                {
                    // Update existing transaction
                    txn.Status = newStatus ?? txn.Status;
                    txn.Brid = rrn ?? txn.Brid;
                    txn.ApiTxnId = apiReferenceId ?? txn.ApiTxnId;
                    txn.ApiMsg = apiMsg ?? txn.ApiMsg;
                    txn.ApiRes = apiResponse ?? txn.ApiRes;
                    txn.ApiReq = apiReq ?? txn.ApiReq;
                    txn.NewBal = newStatus?.ToUpper() == "FAILED"
                        ? Convert.ToString(txn.OldBal)
                        : Convert.ToString(newBal);
                    txn.UpdateDate = DateTime.Now;
                }

                await _context.SaveChangesAsync();

                // Credit commissions if SUCCESS
                if ((newStatus ?? txn.Status).ToUpper() == "SUCCESS")
                {
                    if (isNewCommission && commApiCode != null)
                    {
                        // New-commission path: use ICommissionService for full hierarchy distribution
                        var rtUser = await _context.TblUsers.FindAsync(userKey);
                        if (rtUser != null)
                        {
                            // Credit retailer's commission first
                            if (retailerComm > 0)
                            {
                                await _walletService.CreditAsync(
                                    userKey, $"{data.Name}-{data.Phone}",
                                    txn.Amount ?? 0, retailerComm, 0, 0,
                                    "Commission",
                                    $"{serviceName} Commission For Mobile Recharge {accountNo}",
                                    data.Wlid);
                                txn.Comm = retailerComm;
                            }

                            // Distribute upline (AD → MD → WL → Admin) differentials
                            await _commissionService.DistributeCommissionAsync(
                                txn, rtUser, amount, planIdForComm,
                                commServiceId, commApiCode,
                                $"Commission Credit {serviceName} For Account {accountNo}",
                                opIdInt);
                        }
                    }
                    else
                    {
                        // Legacy path: distribute manually using stored comm values on txn
                        var commissionUsers = new[]
                        {
                             new { Id = txn.UserId, Comm = txn.Comm },
                             new { Id = txn.MdId,   Comm = txn.MdComm },
                             new { Id = txn.AdId,   Comm = txn.AdComm },
                             new { Id = txn.WlId,   Comm = txn.WlComm }
                        }
                        .Where(u => !string.IsNullOrEmpty(u.Id) && u.Id != "0")
                        .Select(u => Convert.ToInt32(u.Id))
                        .ToList();

                        var userInfos = await _context.TblUsers
                            .Where(u => commissionUsers.Contains(u.Id))
                            .Select(u => new { u.Id, Username = u.Name + "-" + u.Phone, u.Wlid })
                            .ToListAsync();

                        foreach (var u in commissionUsers)
                        {
                            var info = userInfos.FirstOrDefault(x => x.Id == u);
                            if (info == null) continue;

                            decimal commAmt = u switch
                            {
                                var x when x == Convert.ToInt32(txn.UserId) => Convert.ToDecimal(txn.Comm),
                                var x when x == Convert.ToInt32(txn.MdId)   => Convert.ToDecimal(txn.MdComm),
                                var x when x == Convert.ToInt32(txn.AdId)   => Convert.ToDecimal(txn.AdComm),
                                var x when x == Convert.ToInt32(txn.WlId)   => Convert.ToDecimal(txn.WlComm),
                                _ => 0
                            };

                            decimal tdsAmt    = commAmt * 0.05m;
                            decimal netAmount = Math.Abs(commAmt - tdsAmt);

                            await _walletService.CreditAsync(
                                info.Id, info.Username,
                                txn.Amount ?? 0, netAmount, commAmt, tdsAmt,
                                "Commission",
                                $"{serviceName} Commission Received For Account no {accountNo}",
                                info.Wlid);
                        }
                    }

                    txn.NewBal = Convert.ToString(balBefore);
                    _context.TransactionDetails.Update(txn);
                    await _context.SaveChangesAsync();
                }
                else if (newStatus?.ToUpper() == "FAILED" && txn != null && flagforUpdate == true)
                {
                    // Refund full face-value amount (works for both commission paths)
                    decimal refundAmount = Convert.ToDecimal(txn.Amount);

                    await _walletService.CreditAsync(
                        userKey, $"{data.Name}-{data.Phone}",
                        txn.Amount ?? 0, refundAmount, 0, 0,
                        serviceName + " Refund",
                        $"{serviceName} Failed Refund For Account {accountNo}",
                        data.Wlid);

                    txn.NewBal = Convert.ToString(txn.OldBal);
                    txn.Status = "Failed";
                    _context.TransactionDetails.Update(txn);
                    _context.SaveChanges();
                }

                return "1";
            }
            catch (Exception ex)
            {

                return ex.ToString();
            }
        }

        /// <summary>
        /// iQore iSPI status check. Queries the indicore live endpoint with the transaction date.
        /// Response format: code|status|customer_ref_no|operator_refno|indicore_refno
        /// </summary>
        public async Task<ResponseSuccess> CheckStatusAsync(string txnId)
        {
            try
            {
                var txn = await _context.TransactionDetails
                    .FirstOrDefaultAsync(t => t.TxnId == txnId || t.ApiTxnId == txnId);

                if (txn == null)
                    return new ResponseSuccess { success = false, message = "Transaction not found" };

                string dbStatus = txn.Status?.ToUpper() ?? "PENDING";
                if (dbStatus == "SUCCESS" || dbStatus == "FAILED" || dbStatus == "REFUNDED")
                    return new ResponseSuccess
                    {
                        success = dbStatus == "SUCCESS",
                        message = $"Transaction already {dbStatus}",
                        txnid = txn.TxnId,
                        apitxnid = txn.ApiTxnId
                    };

                // Only ICORE supports this iSPI status check
                if (!string.Equals(txn.ApiName, "iqore", StringComparison.OrdinalIgnoreCase))
                    return new ResponseSuccess { success = false, message = "Status check not supported for this provider" };

                var baseUrl  = _config["RechargeApis:iCore:BaseUrl"];
                var signature = _config["RechargeApis:iCore:Signature"];
                string txnDate = (txn.ReqDate ?? DateTime.Now).ToString("yyyy-MM-dd");
                string url = $"{baseUrl}/live?signature={signature}&cack={txn.TxnId}&date={txnDate}";

                string apiResponse;
                try
                {
                    using var client = _httpClientFactory.CreateClient();
                    var resp = await client.GetAsync(url);
                    apiResponse = await resp.Content.ReadAsStringAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "iQore status check HTTP call failed for {TxnId}", txnId);
                    return new ResponseSuccess { success = false, message = "Status check API call failed: " + ex.Message };
                }

                // Log the status check
                _context.Apilogs.Add(new Apilog { Apiname = "iCore-StatusCheck", Reqdatae = DateTime.Now, Request = url, Response = apiResponse });
                await _context.SaveChangesAsync();

                var parts = apiResponse.Split('|');
                if (parts.Length < 5)
                    return new ResponseSuccess { success = false, message = "Unexpected status response: " + apiResponse };

                string code        = parts[0].Trim();
                string statusText  = parts[1].Trim().ToUpper();
                string operatorRef = parts[3].Trim();
                string indicoreRef = parts[4].Trim();

                string mappedStatus = (code, statusText) switch
                {
                    ("200", "SUCCESS")   => "SUCCESS",
                    ("202", "FAILED")    => "FAILED",
                    ("200", "SUSPENSE")  => "PENDING",
                    ("200", "PROCESSED") => "PENDING",
                    ("200", "PENDING")   => "PENDING",
                    ("404", _)           => "NOT_FOUND",
                    _                    => "PENDING"
                };

                if (mappedStatus == "NOT_FOUND")
                    return new ResponseSuccess { success = false, message = $"Transaction not found on provider: {statusText}" };

                if (mappedStatus == dbStatus)
                    return new ResponseSuccess
                    {
                        success = mappedStatus == "SUCCESS",
                        message = $"Status unchanged: {mappedStatus}",
                        txnid = txn.TxnId, apitxnid = txn.ApiTxnId
                    };

                txn.Status     = mappedStatus;
                txn.Brid       = operatorRef != "0" ? operatorRef : txn.Brid;
                txn.ApiTxnId   = indicoreRef != "0" ? indicoreRef : txn.ApiTxnId;
                txn.ApiMsg     = statusText;
                txn.UpdateDate = DateTime.Now;
                _context.TransactionDetails.Update(txn);
                await _context.SaveChangesAsync();

                if (mappedStatus == "SUCCESS")
                {
                    var user = await _context.TblUsers.FindAsync(Convert.ToInt32(txn.UserId));
                    if (user != null)
                    {
                        int planIdForComm  = user.CommissionPlanId ?? (int.TryParse(user.PlanId, out int p) ? p : 1);
                        int commServiceId  = txn.ServiceId ?? 1;
                        int? opIdInt       = int.TryParse(txn.OpId, out int oid) ? oid : (int?)null;
                        string commApiCode = "ICORE";

                        decimal rtComm = await _commissionService.GetCommissionFromPlanAsync(
                            planIdForComm, txn.Amount ?? 0, commServiceId, commApiCode, "RT", opIdInt);

                        if (rtComm > 0)
                        {
                            await _walletService.CreditAsync(
                                user.Id, $"{user.Name}-{user.Phone}",
                                txn.Amount ?? 0, rtComm, 0, 0,
                                "Commission",
                                $"Status-Check Commission For TXN {txn.TxnId}",
                                user.Wlid);
                            txn.Comm = rtComm;
                        }

                        await _commissionService.DistributeCommissionAsync(
                            txn, user, txn.Amount ?? 0, planIdForComm,
                            commServiceId, commApiCode,
                            $"Status-Check Commission Recharge For Account {txn.AccountNo}",
                            opIdInt);

                        _context.TransactionDetails.Update(txn);
                        await _context.SaveChangesAsync();
                    }
                }
                else if (mappedStatus == "FAILED")
                {
                    var user = await _context.TblUsers.FindAsync(Convert.ToInt32(txn.UserId));
                    if (user != null)
                    {
                        await _walletService.CreditAsync(
                            user.Id, $"{user.Name}-{user.Phone}",
                            txn.Amount ?? 0, txn.Amount ?? 0, 0, 0,
                            "Recharge Refund",
                            $"Failed Refund For TXN {txn.TxnId}",
                            user.Wlid);
                    }
                    txn.NewBal = Convert.ToString(txn.OldBal);
                    _context.TransactionDetails.Update(txn);
                    await _context.SaveChangesAsync();
                }

                return new ResponseSuccess
                {
                    success = mappedStatus == "SUCCESS",
                    message = $"Status: {mappedStatus}",
                    txnid = txn.TxnId,
                    apitxnid = txn.ApiTxnId,
                    transactiondatetime = DateTime.UtcNow.ToLocalTime().ToString("dd-MM-yyyy hh:mm:ss tt")
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "CheckStatusAsync failed for {TxnId}", txnId);
                return new ResponseSuccess { success = false, message = "ERR:500 " + ex.Message };
            }
        }

    }
}

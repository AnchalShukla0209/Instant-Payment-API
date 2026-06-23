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
        private readonly IConfiguration _config;
        private readonly AppDbContext _context;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IRechargeApiProviderService _provider;
        private readonly ApiTransactionRecoveryService _recoveryService;
        private readonly ILogger<RechargeService> _logger;
        private readonly IWalletService _walletService;

        public RechargeService(AppDbContext context, IRechargeApiProviderService provider, ApiTransactionRecoveryService recoveryService, ILogger<RechargeService> logger, IWalletService walletService)
        {
            _context = context;
            _provider = provider;
            _recoveryService = recoveryService;
            _logger = logger;
            _walletService = walletService;
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

            if (new[] { "1", "2", "10", "11" }.Contains(operatorId))
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
                    Optional1: request.optional1
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
                                _ => "FAILED"
                            };
                        }
                        else
                        {
                            throw new Exception("Invalid response format from iCore API");
                        }
                        break;

                    case "mrobotics":
                        var mObj = JObject.Parse(apiResponse.Split('#')[1]);
                        var mStatus = mObj["status"].ToString().ToLower();
                        finalStatus = (mStatus == "success") ? "SUCCESS" : "FAILED";
                        apiTxnId = mObj["opid"]?.ToString() ?? "";
                        rechargeStatus = mObj["msg"]?.ToString() ?? "";
                        break;

                    case "ambika":
                        var aObj = JObject.Parse(apiResponse);
                        var aStatus = aObj["status"].ToString();
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
                    join s in _context.Tblcommissionslabs
                        on new { u.PlanId, SlabId = slabId } equals new { s.PlanId, s.SlabId }
                        into slabJoin
                    from s in slabJoin.DefaultIfEmpty()
                    where u.Id == userKey
                    select new
                    {
                        u.Id,
                        u.Name,
                        u.Phone,
                        u.Usertype,
                        u.PlanId,
                        u.Adid,
                        u.Mdid,
                        u.Wlid,
                        Slab = s
                    }).FirstOrDefaultAsync();

                if (data == null) return "Invalid User";

                decimal retailerComm = 0, adComm = 0, mdComm = 0, wlComm = 0;

                if (data.Usertype == "RT" && data.Slab != null)
                {
                    retailerComm = (decimal)(data.Slab.CommissionType == "RS" ? data.Slab.Rtshare : amount * data.Slab.Rtshare / 100);
                    adComm = (decimal)(data.Slab.CommissionType == "RS" ? data.Slab.Adshare : amount * data.Slab.Adshare / 100);
                    mdComm = (decimal)(data.Slab.CommissionType == "RS" ? data.Slab.Mdshare : amount * data.Slab.Mdshare / 100);
                    wlComm = (decimal)(data.Slab.CommissionType == "RS" ? data.Slab.WlShare : amount * data.Slab.WlShare / 100);

                    if (data.Adid == "0" && data.Mdid == "0")
                    {
                        wlComm -= retailerComm;
                        adComm = 0;
                        mdComm = 0;
                    }
                    else if (data.Adid == "0" && data.Mdid != "0")
                    {
                        mdComm -= retailerComm;
                        adComm = 0;
                        wlComm -= (decimal)data.Slab.Mdshare;
                    }
                    else if (data.Adid != "0" && data.Mdid != "0")
                    {
                        adComm -= retailerComm;
                        mdComm -= (decimal)data.Slab.Adshare;
                        wlComm -= (decimal)data.Slab.Mdshare;
                    }
                    else if (data.Adid != "0" && data.Mdid == "0")
                    {
                        adComm -= retailerComm;
                        mdComm = 0;
                        wlComm -= (decimal)data.Slab.Adshare;
                    }
                }

                decimal tds = retailerComm * 0.05m;
                decimal cost = amount - retailerComm + tds;

                decimal balBefore = await _walletService.GetBalanceAsync(userKey);
                decimal newBal = balBefore - cost;

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
                        OpId = data.PlanId,
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
                    txn.Status = newStatus.ToUpper() == "PENDING" ? "SUCCESS" : newStatus ?? txn.Status;
                    txn.Brid = rrn ?? txn.Brid;
                    txn.ApiTxnId = apiReferenceId ?? txn.ApiTxnId;
                    txn.ApiMsg = apiMsg ?? txn.ApiMsg;
                    txn.ApiRes = apiResponse ?? txn.ApiRes;
                    txn.ApiReq = apiReq ?? txn.ApiReq;
                    txn.NewBal = newStatus.ToUpper() == "PENDING" || newStatus.ToUpper() == "SUCCESS" ? Convert.ToString(newBal) : Convert.ToString(txn.OldBal);
                    txn.UpdateDate = DateTime.Now;
                }

                await _context.SaveChangesAsync();

                // Credit commissions if SUCCESS
                if ((newStatus ?? txn.Status).ToUpper() == "SUCCESS")
                {
                    var commissionUsers = new[]
                    {
                         new { Id = txn.UserId, Comm = txn.Comm },
                         new { Id = txn.MdId, Comm = txn.MdComm },
                         new { Id = txn.AdId, Comm = txn.AdComm },
                         new { Id = txn.WlId, Comm = txn.WlComm }
                    }
                    .Where(u => !string.IsNullOrEmpty(u.Id) && u.Id != "0")
                    .Select(u => Convert.ToInt32(u.Id))
                    .ToList();

                    var userInfos = await _context.TblUsers
                        .Where(u => commissionUsers.Contains(u.Id))
                        .Select(u => new { u.Id, Username = u.Name + "-" + u.Phone + "", u.Wlid })
                        .ToListAsync();

                    foreach (var u in commissionUsers)
                    {
                        var info = userInfos.FirstOrDefault(x => x.Id == u);
                        if (info == null) continue;

                        decimal commAmt = u switch
                        {
                            var x when x == Convert.ToInt32(txn.UserId) => Convert.ToDecimal(txn.Comm),
                            var x when x == Convert.ToInt32(txn.MdId) => Convert.ToDecimal(txn.MdComm),
                            var x when x == Convert.ToInt32(txn.AdId) => Convert.ToDecimal(txn.AdComm),
                            var x when x == Convert.ToInt32(txn.WlId) => Convert.ToDecimal(txn.WlComm),
                            _ => 0
                        };

                        decimal tdsAmt = commAmt * 0.05m;
                        decimal netAmount = Math.Abs(commAmt - tdsAmt);

                        await _walletService.CreditAsync(
                            info.Id, info.Username,
                            txn.Amount ?? 0, netAmount, commAmt, tdsAmt,
                            "Commission",
                            $"{serviceName} Commission Received For Account no {accountNo}",
                            info.Wlid);
                    }

                    txn.NewBal = Convert.ToString(balBefore - amount);
                    _context.TransactionDetails.Update(txn);
                    await _context.SaveChangesAsync();
                }
                else if (newStatus.ToUpper() == "FAILED" && txn != null && flagforUpdate == true)
                {
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

    }
}

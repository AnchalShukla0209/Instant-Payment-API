using InstantPay.Application.Interfaces;
using InstantPay.Infrastructure.Sql.Entities;
using InstantPay.SharedKernel.Entity;
using InstantPay.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
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

        public RechargeService(AppDbContext context, IHttpClientFactory httpClientFactory, IConfiguration config)
        {
            _context = context;
            _httpClientFactory = httpClientFactory;
            _config = config;
        }

        public async Task<ResponseSuccess> SubmitRechargeAsync(RechargeRequestDto request)
        {
            using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);

            var user = await _context.TblUsers.FindAsync(request.UserId);
            if (user == null)
                return new ResponseSuccess
                {
                    success = false,
                    message = "Invalid User"
                };

            string ServiceId = request.Type == "BLL2" ? "1" : "2";
            var operatorDetails = await _context.Tbloperators
               .Where(x => x.ServiceId == ServiceId
                       && x.OperatorName.Trim().ToLower() == request.Operator.Trim().ToLower()
                       && x.Status.Trim() == "Active")
               .OrderByDescending(x => x.Id)
               .FirstOrDefaultAsync();

            if (operatorDetails == null)
                return new ResponseSuccess
                {
                    success = false,
                    message = "Invalid Operator"
                };

            if (user.TxnPin.Trim() != request.TxnPin.Trim())
                return new ResponseSuccess
                {
                    success = false,
                    message = "Invalid Transaction PIN"
                };

            var latestWallet = await _context.Tbluserbalances
                .Where(x => x.UserId == request.UserId)
                .OrderByDescending(x => x.Id)
                .FirstOrDefaultAsync();

            var currentBalance = latestWallet?.NewBal ?? 0;

            if (currentBalance < request.Amount)
                return new ResponseSuccess
                {
                    success = false,
                    message = "Insufficient balance in wallet. Please add funds."
                };

            decimal newBalance = currentBalance - request.Amount;

            // 🔍 Detect type
            string serviceName = (request.Type == "DTH2") ? "DTH Recharge" : "Mobile Recharge";
            string remarks = serviceName == "BILLPAY"
                ? $"Bill Payment for {request.MobileNumber} | Amount Debit"
                : serviceName== "DTH Recharge" ? $"DTH Recharge for {request.MobileNumber} | Amount Debit": $"Mobile Recharge for {request.MobileNumber} | Amount Debit";

            // 💰 Debit wallet before API call
            _context.Tbluserbalances.Add(new Tbluserbalance
            {
                UserId = request.UserId,
                UserName = user.Name,
                OldBal = currentBalance,
                Amount = request.Amount,
                NewBal = newBalance,
                TxnType = serviceName,
                CrdrType = "DR",
                Remarks = remarks,
                WlId = "1",
                Txndate = DateTime.UtcNow,
                TxnAmount = request.Amount
            });
            await _context.SaveChangesAsync();

            // 🔗 Call Recharge/Bill API
            var signaturekey = _config["recharge:signature"];
            var query = $"signature={signaturekey}" +
                        $"&rnum={request.MobileNumber}&ramt={request.Amount}&optr={request.operatorCode}" +
                        $"&type={request.Type}&cack={request.CustomerRefNo}" +
                        $"&optional=na&optional1=na&optional2=na&optional3=na";

            var client = _httpClientFactory.CreateClient();
            var baseUrl = _config["recharge:Baseurl"];
            var response = await client.PostAsync($"{baseUrl}/live?{query}", null);
            var apiResponse = await response.Content.ReadAsStringAsync();

            // Log API call
            _context.Apilogs.Add(new Apilog
            {
                Request = query,
                Response = apiResponse,
                Apiname = serviceName,
                Reqdatae = DateTime.UtcNow
            });
            await _context.SaveChangesAsync();

            // ✅ Parse API Response
            var responseParts = apiResponse.Split('|');
            if (responseParts.Length < 5)
            {
                await transaction.RollbackAsync();
                return new ResponseSuccess
                {
                    success = false,
                    message = "Invalid response format from Recharge/BillPay API."
                };
            }

            var rechargeStatusCode = responseParts[0].Trim();
            var rechargeStatus = responseParts[1].Trim();
            var customerRefNo = responseParts[2].Trim();
            var operatorRefNo = responseParts[3].Trim();
            var apiTxnId = responseParts[4].Trim();

            var failedCodes = new List<string> { "202", "203", "204", "403", "503" };
            var PendingCodes = new List<string> { "201"};
            var SuccessCodes = new List<string> { "200"};
            bool isFailed = failedCodes.Contains(rechargeStatusCode);
            string Status = SuccessCodes.Contains(rechargeStatusCode)==true? "Success": PendingCodes.Contains(rechargeStatusCode) == true? "Pending": "Failed";
            // 💸 Wallet Refund if Failed
            if (isFailed)
            {

                _context.Tbluserbalances.Add(new Tbluserbalance
                {
                    UserId = request.UserId,
                    UserName = user.Name,
                    OldBal = newBalance,
                    Amount = request.Amount,
                    NewBal = currentBalance,
                    TxnType = $"{serviceName}_REFUND",
                    CrdrType = "CR",
                    Remarks = $"REFUND for failed {serviceName.ToLower()} of {request.MobileNumber}",
                    WlId = "1",
                    Txndate = DateTime.UtcNow,
                    TxnAmount = request.Amount
                });
            }

            // 🧾 Transaction log
            _context.TransactionDetails.Add(new TransactionDetail
            {
                UserId = Convert.ToString(request.UserId),
                UserName = user.Name,
                WlId = "1",
                MdId = "0",
                AdId = "0",
                TxnId = customerRefNo,
                ServiceName = serviceName,
                OperatorName = request.Operator,
                OpId = Convert.ToString(operatorDetails.Id),
                Mobileno = request.MobileNumber,
                OldBal = currentBalance,
                Amount = request.Amount,
                Comm = 0,
                Charge = 0,
                Cost = request.Amount,
                NewBal = isFailed ? currentBalance.ToString() : newBalance.ToString(),
                Status = Status,
                TxnType = isFailed ? "Refund" : "Debit",
                ApiTxnId = apiTxnId,
                ApiName = "INDICORE",
                ReqDate = DateTime.UtcNow,
                UpdateDate = DateTime.UtcNow,
                CustomerName = "NA",
                AccountNo = request.MobileNumber,
                ComingFrom = "Web",
                ServiceId = Convert.ToInt32(ServiceId)
            });

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return new ResponseSuccess
            {
                success = !isFailed,
                message = isFailed ? $"{serviceName} failed: {rechargeStatus}" : $"{serviceName} successful.",
                txnid = customerRefNo,
                apitxnid = apiTxnId,
                transactiondatetime = DateTime.UtcNow.ToLocalTime().ToString("dd-MM-yyyy hh:mm:ss tt")
            };
        }


    }

}

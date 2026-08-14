using InstantPay.Application.DTOs;
using InstantPay.Application.Interfaces;
using InstantPay.Infrastructure.Sql.Entities;
using InstantPay.SharedKernel.Entity;
using Microsoft.EntityFrameworkCore;
using Razorpay.Api;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InstantPay.Application.Services
{
    public class ReportService : IReportService
    {
        private readonly AppDbContext _context;
        private readonly IEmailService _emailService;
        private readonly IWalletService _walletService;
        public ReportService(AppDbContext context, IEmailService emailService, IWalletService walletService)
        {
            _context = context;
            _emailService = emailService;
            _walletService = walletService;
        }

        public async Task<PaginatedTxnResultDto> GetTransactionReportAsync(string serviceType, string status, string dateFrom, string dateTo, int userId, int pageIndex = 1, int pageSize = 50, string commonsearch = "", int ispaginationenabled = 1)
        {


            serviceType = serviceType?.Trim().ToUpper();
            commonsearch = commonsearch?.Trim().ToLower();

            status = status?.Trim().ToUpper();
            var userdata = _context.TblUsers.Where(id => id.Id == userId).FirstOrDefault();
            IQueryable<TxnReportData> query = Enumerable.Empty<TxnReportData>().AsQueryable();
            int flagForTrans = 0;

            if (serviceType == "QR CODE" || serviceType == "ONLINE PAYMENT")
            {
                string gatewayType = serviceType == "QR CODE" ? "UPI" : "Razorpay";

                query = from tonp in _context.Tblonlinepayments
                        join tum in _context.TblUsers on tonp.Mdid equals Convert.ToString(tum.Id) into tumJoin
                        from tum in tumJoin.DefaultIfEmpty()

                        join tua in _context.TblUsers on tonp.AdId equals Convert.ToString(tua.Id) into tuaJoin
                        from tua in tuaJoin.DefaultIfEmpty()

                        join tud in _context.TblUsers on tonp.UserKey equals Convert.ToString(tud.Id) into tudJoin
                        from tud in tudJoin.DefaultIfEmpty()

                        where tonp.Gatwaytype == gatewayType
                              && (string.IsNullOrEmpty(dateFrom) || tonp.ReqDate.Value.Date >= DateTime.Parse(dateFrom).Date)
                              && (string.IsNullOrEmpty(dateTo) || tonp.ReqDate.Value.Date <= DateTime.Parse(dateTo).Date)
                              && (string.IsNullOrEmpty(status) || tonp.Status.ToUpper() == status)
                              && ((userId == 0) || (tonp.UserKey == Convert.ToString(userId)))

                              && (string.IsNullOrEmpty(commonsearch)
                                  || (tonp.AadharCard ?? "").ToLower().Contains(commonsearch)
                                  || (tonp.Pancard ?? "").ToLower().Contains(commonsearch)
                                  || (tonp.Gatwaytype ?? "").ToLower().Contains(commonsearch)
                                  || (tonp.OrderId ?? "").ToLower().Contains(commonsearch)
                                  || tonp.ReqBy.ToString().ToLower().Contains(commonsearch)
                                 )

                        select new TxnReportData
                        {
                            Id = tonp.Id,
                            TXN_ID = Convert.ToString(tonp.TxnId),
                            BankRefNo = tonp.OrderId,
                            UserName = tud.Name+"-"+tud.Phone ?? string.Empty,
                            OperatorName = tonp.PanName,
                            AccountNo = tonp.Pancard,
                            OpeningBal = 0,
                            Amount = tonp.Amount,
                            Closing = 0,
                            Status = tonp.Status,
                            APIName = tonp.MobileNo,
                            ComingFrom = Convert.ToString(tonp.Paymentid),
                            MasterDistributor = tum.Name ?? string.Empty,
                            Distributor = tua.Name ?? string.Empty,
                            TimeStamp = tonp.ReqDate,
                            UpdatedTime = tonp.ResDate,
                            Success = string.Empty,
                            Failed = string.Empty,
                            APIRes = ispaginationenabled > 0 ? tonp.Apiresponse ?? string.Empty : "",
                            flagforTrans = 1,
                            servicename = tonp.Gatwaytype.ToUpper() == "UPI" ? "QR CODE" : tonp.Gatwaytype.ToUpper() ?? string.Empty,
                            CustomerMobile = tonp.Cardno,
                            BeneName = tonp.Cardtype,
                            Transactionid = tonp.AadharCard,
                            BRId = tonp.Rrn
                        };
            }

            else if (serviceType == "LESSER REPORT")
            {
                query = from t in _context.Tbluserbalances
                        join ud in _context.TblUsers on t.UserId equals ud.Id
                        where (string.IsNullOrEmpty(dateFrom) || t.Txndate.Value.Date >= DateTime.Parse(dateFrom).Date)
                           && (string.IsNullOrEmpty(dateTo) || t.Txndate.Value.Date <= DateTime.Parse(dateTo).Date)
                           && (userId == 0 || t.UserId == userId)
                            && (string.IsNullOrEmpty(commonsearch)
                                  || (t.Remarks ?? "").ToLower().Contains(commonsearch)
                                  || (t.Amount.ToString() ?? "").ToLower().Contains(commonsearch)
                                  || (t.TxnAmount.ToString() ?? "").ToLower().Contains(commonsearch)
                                  || (t.TxnType ?? "").ToLower().Contains(commonsearch)
                                 )

                        select new TxnReportData
                        {
                            Id = t.Id,
                            TXN_ID = string.Empty,
                            BankRefNo = string.Empty,
                            UserName = ud.Name + "-"+ ud.Phone ?? string.Empty,
                            OperatorName = string.Empty,
                            AccountNo = string.Empty,
                            OpeningBal = t.OldBal,
                            Amount = t.TxnAmount,
                            Closing = t.NewBal,
                            Status = t.CrdrType,
                            APIName = t.TxnType,
                            ComingFrom = string.Empty,
                            MasterDistributor = string.Empty,
                            Distributor = string.Empty,
                            TimeStamp = t.Txndate,
                            UpdatedTime = null,
                            Success = string.Empty,
                            Failed = string.Empty,
                            APIRes = ispaginationenabled > 0 ? t.Remarks ?? string.Empty : "",
                            flagforTrans = 0,
                            BeneName = Convert.ToString(t.SurCom),
                            CustomerMobile = Convert.ToString(t.Tds),
                            servicename = Convert.ToString(t.TxnAmount)
                        };
            }

            else if (serviceType == "ADMIN LESSER REPORT")
            {
                query = from t in _context.TblWlbalances
                        join ud in _context.TblWlUsers on Convert.ToInt32(t.UserId) equals ud.Id
                        where (string.IsNullOrEmpty(dateFrom) || t.Txndate.Value.Date >= DateTime.Parse(dateFrom).Date)
                           && (string.IsNullOrEmpty(dateTo) || t.Txndate.Value.Date <= DateTime.Parse(dateTo).Date)
                           && (userId == 0 || t.UserId == Convert.ToString(userId))

                        select new TxnReportData
                        {
                            Id = t.Id,
                            TXN_ID = string.Empty,
                            BankRefNo = string.Empty,
                            UserName = ud.UserName + "-" + ud.Phone ?? string.Empty,
                            OperatorName = string.Empty,
                            AccountNo = string.Empty,
                            OpeningBal = t.OldBal,
                            Amount = t.TxnAmount,
                            Closing = t.NewBal,
                            Status = t.CrdrType,
                            APIName = t.TxnType,
                            ComingFrom = string.Empty,
                            MasterDistributor = string.Empty,
                            Distributor = string.Empty,
                            TimeStamp = t.Txndate,
                            UpdatedTime = null,
                            Success = string.Empty,
                            Failed = string.Empty,
                            APIRes = ispaginationenabled > 0 ? t.Remarks ?? string.Empty : "",
                            flagforTrans = 0,
                            BeneName = Convert.ToString(t.SurComm),
                            CustomerMobile = Convert.ToString(t.Tds),
                            servicename = Convert.ToString(t.TxnAmount)
                        };
            }

            else if (serviceType == "SUPERADMIN LESSER REPORT")
            {
                query = from t in _context.TblSuperAdminUserBalances
                        join ud in _context.TblSuperadmins on Convert.ToInt32(t.UserId) equals ud.Id
                        where (string.IsNullOrEmpty(dateFrom) || t.Txndate.Value.Date >= DateTime.Parse(dateFrom).Date)
                           && (string.IsNullOrEmpty(dateTo) || t.Txndate.Value.Date <= DateTime.Parse(dateTo).Date)
                           && (userId == 0 || t.UserId == userId)

                        select new TxnReportData
                        {
                            Id = t.Id,
                            TXN_ID = string.Empty,
                            BankRefNo = string.Empty,
                            UserName = ud.Name + "-" + ud.Username ?? string.Empty,
                            OperatorName = string.Empty,
                            AccountNo = string.Empty,
                            OpeningBal = t.OldBal,
                            Amount = t.TxnAmount,
                            Closing = t.NewBal,
                            Status = t.CrdrType,
                            APIName = t.TxnType,
                            ComingFrom = string.Empty,
                            MasterDistributor = string.Empty,
                            Distributor = string.Empty,
                            TimeStamp = t.Txndate,
                            UpdatedTime = null,
                            Success = string.Empty,
                            Failed = string.Empty,
                            APIRes = ispaginationenabled > 0 ? t.Remarks ?? string.Empty : "",
                            flagforTrans = 0,
                            BeneName = Convert.ToString(t.SurComm),
                            CustomerMobile = Convert.ToString(t.Tds),
                            servicename = Convert.ToString(t.TxnAmount)
                        };
            }

            else if (serviceType == "SETTLEMENT")
            {
                flagForTrans = 1;
                query = from tds in _context.SettlementWithdrawals
                        join tud in _context.TblUsers on tds.UserId equals Convert.ToString(tud.Id) into tudJoin
                        from tud in tudJoin.DefaultIfEmpty()

                        where (userId == 0 || tds.UserId == Convert.ToString(userId))
                              && (serviceType == "ALL SERVICE" || serviceType == "SETTLEMENT")
                              && (string.IsNullOrEmpty(dateFrom) || tds.CreatedAt.Date >= DateTime.Parse(dateFrom).Date)
                              && (string.IsNullOrEmpty(dateTo) || tds.CreatedAt.Date <= DateTime.Parse(dateTo).Date)
                              && (string.IsNullOrEmpty(status) || tds.PayoutStatus.Trim().ToUpper() == status)
                              && (string.IsNullOrEmpty(commonsearch)
                                  || (tds.WithdrawalType ?? "").ToLower().Contains(commonsearch)
                                  || (Convert.ToString(tds.Amount) ?? "").ToLower().Contains(commonsearch)
                                  || (tds.BankAccount ?? "").ToLower().Contains(commonsearch)
                                  || (tds.RRN ?? "").ToLower().Contains(commonsearch)
                                  || tds.PayoutTransactionId.ToString().ToLower().Contains(commonsearch)
                                 )

                        select new TxnReportData
                        {
                            Id = tds.Id,
                            TXN_ID = tds.PayoutTransactionId.ToString(),
                            BankRefNo = tds.BankName ?? string.Empty,
                            BRId = tds.RRN ?? string.Empty,
                            UserName = tud.Name + "-" + tud.Phone ?? tud.Username ?? string.Empty,
                            OperatorName =  tds.Charge.ToString()??"",
                            AccountNo = tds.BankAccount ?? string.Empty,
                            OpeningBal = 0,
                            Amount = tds.Amount,
                            Closing = 0,
                            Status = tds.PayoutStatus ?? string.Empty,
                            APIName = "Settlement" ?? string.Empty,
                            ComingFrom = tds.ComingFrom ?? string.Empty,
                            MasterDistributor = string.Empty,
                            Distributor = string.Empty,
                            TimeStamp = tds.CreatedAt,
                            UpdatedTime = tds.WithdrawalDate,
                            Success = tds.Ifsc ?? "",
                            Failed = string.Empty,
                            APIRes = ispaginationenabled > 0 ? tds.PayoutResponse ?? string.Empty : "",
                            flagforTrans = 1,
                            BeneName = tds.BeneName ?? string.Empty,
                            CustomerMobile = tds.BenePhone ?? string.Empty,
                            servicename = "Settlement"+ tds.WithdrawalType ?? string.Empty,
                            Transactionid = tds.PayoutReferenceId ?? string.Empty,
                        };
            }
            else
            {
                flagForTrans = 1;

                query = from tds in _context.TransactionDetails
                        join tum in _context.TblUsers on tds.MdId equals Convert.ToString(tum.Id) into tumJoin
                        from tum in tumJoin.DefaultIfEmpty()

                        join tua in _context.TblUsers on tds.AdId equals Convert.ToString(tua.Id) into tuaJoin
                        from tua in tuaJoin.DefaultIfEmpty()

                        join tud in _context.TblUsers on tds.UserId equals Convert.ToString(tud.Id) into tudJoin
                        from tud in tudJoin.DefaultIfEmpty()

                        where (userId == 0 || tds.UserId == Convert.ToString(userId))
                              && (serviceType == "ALL SERVICE" || tds.ServiceName.Trim().ToUpper() == serviceType)
                              && (string.IsNullOrEmpty(dateFrom) || tds.ReqDate.Value.Date >= DateTime.Parse(dateFrom).Date)
                              && (string.IsNullOrEmpty(dateTo) || tds.ReqDate.Value.Date <= DateTime.Parse(dateTo).Date)
                              && (string.IsNullOrEmpty(status) || tds.Status.Trim().ToUpper() == status)
                              && (string.IsNullOrEmpty(commonsearch)
                                  || (tds.AccountNo ?? "").ToLower().Contains(commonsearch)
                                  || (tds.Mobileno ?? "").ToLower().Contains(commonsearch)
                                  || (tds.CustomerName ?? "").ToLower().Contains(commonsearch)
                                  || (tds.Brid ?? "").ToLower().Contains(commonsearch)
                                  || tds.TransId.ToString().ToLower().Contains(commonsearch)
                                 )

                        select new TxnReportData
                        {
                            Id = tds.TransId,
                            TXN_ID = tds.TransId.ToString(),
                            BankRefNo = tds.BankName ?? string.Empty,
                            BRId = tds.Brid ?? string.Empty,
                            UserName = tud.Name + "-" + tud.Phone ?? tud.Username ?? string.Empty,
                            OperatorName = tds.OperatorName ?? string.Empty,
                            AccountNo = tds.AccountNo ?? string.Empty,
                            OpeningBal = tds.OldBal,
                            Amount = tds.Amount,
                            Closing = Convert.ToDecimal(tds.NewBal),
                            Status = tds.Status ?? string.Empty,
                            APIName = tds.ApiName ?? string.Empty,
                            ComingFrom = tds.ComingFrom ?? string.Empty,
                            MasterDistributor = tum.Name ?? string.Empty,
                            Distributor = tua.Name ?? string.Empty,
                            TimeStamp = tds.ReqDate,
                            UpdatedTime = tds.UpdateDate,
                            Success = string.Empty,
                            Failed = string.Empty,
                            APIRes = ispaginationenabled > 0 ? tds.ApiRes ?? string.Empty : "",
                            flagforTrans = 1,
                            BeneName = tds.CustomerName ?? string.Empty,
                            CustomerMobile = tds.Mobileno ?? string.Empty,
                            servicename = tds.ServiceName ?? string.Empty,
                            Transactionid = tds.TxnId ?? string.Empty
                        };
            }

            var baseQuery = query.AsNoTracking();

            // 🔹 Total Count (FULL filtered data)
            var totalTransactions = await baseQuery.CountAsync();

            // 🔹 Total Amount / Balance (FULL filtered data)
            var totalAmount = await baseQuery
                .Select(x => (decimal?)x.Amount)
                .SumAsync() ?? 0;


            var paginated = new List<TxnReportData>();
            if (ispaginationenabled > 0)
            {
                paginated = await query
                    .OrderByDescending(x => x.Id)
                    .Skip((pageIndex - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();
            }
            else
            {
                paginated = await query
                    .OrderByDescending(x => x.Id)
                    .ToListAsync();
            }

            return new PaginatedTxnResultDto
            {
                Data = paginated,
                TotalTransactions = totalTransactions,
                TotalAmount = totalAmount,
                FlagForTrans = flagForTrans
            };
        }


        public async Task<TxnDetailsData> GetTxnDetails(int txnId, string ServiceName)
        {
            var txn = new TxnDetailsData();
            if (string.Equals(ServiceName?.Trim(), "RAZORPAY", StringComparison.OrdinalIgnoreCase) || string.Equals(ServiceName?.Trim(), "QR CODE", StringComparison.OrdinalIgnoreCase))
            {
                txn = await (from t in _context.Tblonlinepayments
                             where t.Id == txnId
                             select new TxnDetailsData
                             {
                                 TransId = t.Id,
                                 Status = t.Status ?? string.Empty,
                                 AccountNo = t.AadharCard ?? t.MobileNo,
                                 Cost = t.TransferAmt,
                                 ServiceName = t.Gatwaytype.ToLower().Trim() == "razorpay" ? "Online Payment" : "QR CODE" ?? string.Empty,
                                 UserName = t.UserName ?? string.Empty,
                                 UserId = t.UserKey ?? string.Empty
                             }).FirstOrDefaultAsync();
            }
            else if (string.Equals(ServiceName?.Trim(), "SettlementAEPS", StringComparison.OrdinalIgnoreCase) || string.Equals(ServiceName?.Trim(), "SettlementRazorpay", StringComparison.OrdinalIgnoreCase) || string.Equals(ServiceName?.Trim(), "SettlementMATM", StringComparison.OrdinalIgnoreCase))
            {
                txn = await (from t in _context.SettlementWithdrawals
                             where t.Id == txnId
                             select new TxnDetailsData
                             {
                                 TransId = t.Id,
                                 Status = t.PayoutStatus ?? string.Empty,
                                 AccountNo = t.BankAccount ?? "",
                                 Cost = t.Amount,
                                 ServiceName = "Settlement"+t.WithdrawalType ?? "Settlement",
                                 UserName = t.UserName ?? string.Empty,
                                 UserId = t.UserId ?? string.Empty
                             }).FirstOrDefaultAsync();
            }
            else
            {
                txn = await (from t in _context.TransactionDetails
                             where t.TransId == txnId
                             select new TxnDetailsData
                             {
                                 TransId = t.TransId,
                                 Status = t.Status ?? string.Empty,
                                 AccountNo = t.AccountNo ?? string.Empty,
                                 Cost = t.Cost == 0 ? t.Amount : t.Cost,
                                 ServiceName = t.ServiceName ?? string.Empty,
                                 UserName = t.UserName ?? string.Empty,
                                 UserId = t.UserId ?? string.Empty
                             }).FirstOrDefaultAsync();
            }


            if (txn == null)
            {
                return null;
            }

            return txn;
        }

        public async Task<TxnUpdateResponse> UpdateTxnStatus(TxnUpdateRequest request, int actionById)
        {
            // 2. Validate Refund PIN
            var admin = await _context.TblSuperadmins.FirstOrDefaultAsync(x => x.Id == actionById);
            if (admin == null)
            {
                return (new TxnUpdateResponse { ErrorMsg = "User Not Found in SuperAdmin", Flag = false });
            }

            using var dbTxn = await _context.Database.BeginTransactionAsync();
            try
            {
                if (!string.Equals(request.Status?.Trim(), "REFUND", StringComparison.OrdinalIgnoreCase))
                {
                    bool isPinValid = string.Equals(request.TxnPin?.Trim(), admin.TxnPin?.Trim(), StringComparison.OrdinalIgnoreCase)
                             || string.Equals(request.Status?.Trim(), "SUCCESS", StringComparison.OrdinalIgnoreCase);

                    if (!isPinValid)
                    {
                        return (new TxnUpdateResponse { ErrorMsg = "Invalid Txn Pin", Flag = false });
                    }

                    var user = await _context.TblUsers.FirstOrDefaultAsync(u => u.Id == request.UserId);

                    if (user == null)
                    {
                        return (new TxnUpdateResponse { ErrorMsg = "Invalid User", Flag = false });
                    }

                    decimal currentBal = await _walletService.GetBalanceAsync(request.UserId);
                    decimal estimatedNewBal = currentBal + request.Amount;

                    if (string.Equals(request.ServiceName?.Trim(), "QR CODE", StringComparison.OrdinalIgnoreCase) || string.Equals(request.ServiceName?.Trim(), "Online Payment", StringComparison.OrdinalIgnoreCase))
                    {
                        var txn = await _context.Tblonlinepayments.FirstOrDefaultAsync(t => t.Id == request.TransId);
                        if (txn == null)
                        {
                            return (new TxnUpdateResponse { ErrorMsg = "Transaction not found", Flag = false });
                        }

                        txn.Status = request.Status;
                        txn.ResDate = DateTime.Now;
                        if (string.Equals(request.Status?.Trim(), "SUCCESS", StringComparison.OrdinalIgnoreCase))
                        {
                            txn.Rrn = request.AccountNo;
                        }
                        _context.Tblonlinepayments.Update(txn);
                        await _context.SaveChangesAsync();

                        // Send email notification for settlement status change
                        var settlementUser = await _context.TblUsers.FirstOrDefaultAsync(u => u.Id.ToString() == txn.UserKey);
                        if (settlementUser != null)
                        {
                            _ = _emailService.SendTransactionStatusEmailAsync(
                                txn.TxnId?.ToString() ?? txn.Paymentid.ToString(),
                                txn.AadharCard ?? "N/A",
                                txn.Gatwaytype ?? "Settlement",
                                (decimal)txn.Amount,
                                settlementUser.Name + "-" + settlementUser.Phone,
                                (DateTime)txn.ReqDate,
                                request.Status,
                                request.AccountNo
                            );
                        }
                    }
                    else if (string.Equals(request.ServiceName?.Trim(), "SETTLEMENT", StringComparison.OrdinalIgnoreCase) || string.Equals(request.ServiceName?.Trim(), "SettlementAEPS", StringComparison.OrdinalIgnoreCase) || string.Equals(request.ServiceName?.Trim(), "SettlementRazorpay", StringComparison.OrdinalIgnoreCase) || string.Equals(request.ServiceName?.Trim(), "SettlementMATM", StringComparison.OrdinalIgnoreCase))
                    {
                        var settlement = await _context.SettlementWithdrawals.FirstOrDefaultAsync(s => s.Id == request.TransId);
                        if (settlement == null)
                        {
                            return (new TxnUpdateResponse { ErrorMsg = "Settlement transaction not found", Flag = false });
                        }

                        settlement.PayoutStatus = request.Status;
                        settlement.WithdrawalDate = DateTime.Now;
                        if (string.Equals(request.Status?.Trim(), "SUCCESS", StringComparison.OrdinalIgnoreCase))
                        {
                            settlement.RRN = request.AccountNo;
                        }
                        _context.SettlementWithdrawals.Update(settlement);
                        await _context.SaveChangesAsync();

                        // Send email notification for settlement status change
                        var settlementUser = await _context.TblUsers.FirstOrDefaultAsync(u => u.Id.ToString() == settlement.UserId);
                        if (settlementUser != null)
                        {
                            _ = _emailService.SendTransactionStatusEmailAsync(
                                settlement.PayoutTransactionId?.ToString() ?? settlement.Id.ToString(),
                                settlement.BankAccount ?? "N/A",
                                settlement.WithdrawalType ?? "Settlement",
                                settlement.Amount,
                                settlementUser.Name+"-"+settlementUser.Phone,
                                settlement.CreatedAt,
                                request.Status,
                                settlement.RRN
                            );
                        }
                    }
                    else
                    {
                        var txn = await _context.TransactionDetails.FirstOrDefaultAsync(t => t.TransId == request.TransId);
                        if (txn == null)
                        {
                            return (new TxnUpdateResponse { ErrorMsg = "Transaction not found", Flag = false });
                        }

                        txn.Status = request.Status;
                        txn.AdminRemarks = request.Remarks;
                        txn.UpdateDate = DateTime.Now;
                        if (string.Equals(request.Status?.Trim(), "SUCCESS", StringComparison.OrdinalIgnoreCase))
                        {
                            txn.OldBal = currentBal;
                            txn.NewBal = estimatedNewBal.ToString();
                            txn.Brid = request.AccountNo;
                        }
                        _context.TransactionDetails.Update(txn);
                        await _context.SaveChangesAsync();

                        // Send email notification for transaction status change
                        if (user != null)
                        {
                            _ = _emailService.SendTransactionStatusEmailAsync(
                                txn.TxnId ?? txn.TransId.ToString(),
                                txn.AccountNo ?? "N/A",
                                txn.ServiceName ?? "Transaction",
                                (decimal)txn.Amount,
                                user.Name+"-"+user.Phone,
                                txn.ReqDate ?? DateTime.Now,
                                request.Status,
                                txn.Brid
                            );
                        }
                    }


                    if (string.Equals(request.Status?.Trim(), "SUCCESS", StringComparison.OrdinalIgnoreCase) && (string.Equals(request.ServiceName?.Trim(), "AEPS", StringComparison.OrdinalIgnoreCase) || string.Equals(request.ServiceName?.Trim(), "MATM", StringComparison.OrdinalIgnoreCase) || string.Equals(request.ServiceName?.Trim(), "Online Payment", StringComparison.OrdinalIgnoreCase) || string.Equals(request.ServiceName?.Trim(), "QR CODE", StringComparison.OrdinalIgnoreCase)))
                    {
                        await _walletService.CreditAsync(
                            request.UserId,
                            user.Name + "-" + user.Phone,
                            request.Amount, request.Amount, 0, 0,
                            request.ServiceName,
                            $"{request.Remarks} || {request.ServiceName}",
                            user.Wlid);
                    }
                }

                if (string.Equals(request.Status?.Trim(), "REFUND", StringComparison.OrdinalIgnoreCase))
                {
                    string AccountNo = "";
                    bool isPinValid = string.Equals(request.TxnPin?.Trim(), admin.Refundpin?.Trim(), StringComparison.OrdinalIgnoreCase)
                             || string.Equals(request.Status?.Trim(), "SUCCESS", StringComparison.OrdinalIgnoreCase);

                    if (!isPinValid)
                    {
                        return (new TxnUpdateResponse { ErrorMsg = "Invalid Refund Pin", Flag = false });
                    }

                    var user = await _context.TblUsers.FirstOrDefaultAsync(u => u.Id == request.UserId);
                    if (user != null)
                    {

                        if (string.Equals(request.ServiceName?.Trim(), "QR CODE", StringComparison.OrdinalIgnoreCase) || string.Equals(request.ServiceName?.Trim(), "Online Payment", StringComparison.OrdinalIgnoreCase))
                        {
                            var txn = await _context.Tblonlinepayments.FirstOrDefaultAsync(t => t.Id == request.TransId);
                            if (txn == null)
                            {
                                return (new TxnUpdateResponse { ErrorMsg = "Transaction not found", Flag = false });
                            }
                            AccountNo = txn.Cardno + "||" + txn.Paymentid + "||" + txn.Rrn;
                            txn.Status = "REFUNDED";
                            txn.ResDate = DateTime.Now;
                            _context.Tblonlinepayments.Update(txn);
                            await _context.SaveChangesAsync();
                        }
                        else if (string.Equals(request.ServiceName?.Trim(), "SETTLEMENT", StringComparison.OrdinalIgnoreCase) || string.Equals(request.ServiceName?.Trim(), "SettlementAEPS", StringComparison.OrdinalIgnoreCase) || string.Equals(request.ServiceName?.Trim(), "SettlementRazorpay", StringComparison.OrdinalIgnoreCase) || string.Equals(request.ServiceName?.Trim(), "SettlementMATM", StringComparison.OrdinalIgnoreCase))
                        {
                            var settlement = await _context.SettlementWithdrawals.FirstOrDefaultAsync(s => s.Id == request.TransId);
                            if (settlement == null)
                            {
                                return (new TxnUpdateResponse { ErrorMsg = "Settlement transaction not found", Flag = false });
                            }
                            AccountNo = settlement.BankAccount + "||" + settlement.PayoutTransactionId+ "||" + settlement.RRN;
                            settlement.PayoutStatus = "REFUNDED";
                            settlement.WithdrawalDate = DateTime.Now;
                            _context.SettlementWithdrawals.Update(settlement);
                            await _context.SaveChangesAsync();
                        }
                        else
                        {
                            var txn = await _context.TransactionDetails.FirstOrDefaultAsync(t => t.TransId == request.TransId);
                            if (txn == null)
                            {
                                return (new TxnUpdateResponse { ErrorMsg = "Transaction not found", Flag = false });
                            }
                            AccountNo = txn.AccountNo + "||" + txn.TxnId + "||" + txn.Brid;
                            txn.Status = "REFUNDED";
                            txn.AdminRemarks = request.Remarks;
                            txn.UpdateDate = DateTime.Now;
                            _context.TransactionDetails.Update(txn);
                            await _context.SaveChangesAsync();
                        }

                        await _walletService.CreditAsync(
                            request.UserId,
                            user.Name + "-" + user.Phone,
                            request.Amount, request.Amount, 0, 0,
                            request.ServiceName,
                            $"Reverse Amount Credit For {request.ServiceName + "||" + AccountNo}",
                            user.Wlid);

                        // Send email notification for settlement status change
                        if (user != null)
                        {
                            _ = _emailService.SendTransactionStatusEmailAsync(
                                request.TransId.ToString(),
                                 "",
                                request.ServiceName ?? "Settlement",
                                request.Amount,
                                user.Name + "-" + user.Phone,
                                DateTime.Now,
                                request.Status,
                                ""
                            );
                        }
                    }
                }

                await dbTxn.CommitAsync();
                if (string.Equals(request.Status?.Trim(), "REFUND", StringComparison.OrdinalIgnoreCase))
                {
                    return (new TxnUpdateResponse { ErrorMsg = "Refund has been initiated Successfully", Flag = true });
                }
                return (new TxnUpdateResponse { ErrorMsg = "Transaction Details Updated Successfully", Flag = true });
            }
            catch (Exception ex)
            {
                await dbTxn.RollbackAsync();
                return new TxnUpdateResponse { ErrorMsg = "Internal Server Error", Flag = false };
            }
        }

        public async Task<PaginatedTxnResultDto> GetUserTransactionReportAsync(
   string serviceType, string status, string dateFrom, string dateTo,
   int userId, string username, int pageIndex = 1, int pageSize = 50, string commonsearch = "", int ispaginationenabled = 1)
        {
            serviceType = serviceType?.Trim().ToUpper();
            commonsearch = commonsearch?.Trim().ToLower();

            status = status?.Trim().ToUpper();
            var userdata = _context.TblUsers.Where(id => id.Id == userId).FirstOrDefault();
            IQueryable<TxnReportData> query = Enumerable.Empty<TxnReportData>().AsQueryable();
            int flagForTrans = 0;

            if (serviceType == "QR CODE" || serviceType == "ONLINE PAYMENT")
            {
                string gatewayType = serviceType == "QR CODE" ? "UPI" : "Razorpay";

                query = from tonp in _context.Tblonlinepayments
                        join tum in _context.TblUsers on tonp.Mdid equals Convert.ToString(tum.Id) into tumJoin
                        from tum in tumJoin.DefaultIfEmpty()

                        join tua in _context.TblUsers on tonp.AdId equals Convert.ToString(tua.Id) into tuaJoin
                        from tua in tuaJoin.DefaultIfEmpty()

                        join tud in _context.TblUsers on tonp.UserKey equals Convert.ToString(tud.Id) into tudJoin
                        from tud in tudJoin.DefaultIfEmpty()

                        where tonp.Gatwaytype == gatewayType
                              && (string.IsNullOrEmpty(dateFrom) || tonp.ReqDate.Value.Date >= DateTime.Parse(dateFrom).Date)
                              && (string.IsNullOrEmpty(dateTo) || tonp.ReqDate.Value.Date <= DateTime.Parse(dateTo).Date)
                              && (string.IsNullOrEmpty(status) || tonp.Status.ToUpper() == status)
                              && (tonp.UserKey == Convert.ToString(userId))
                              && (tonp.UserName.ToLower().Trim() == Convert.ToString(username + "-" + userdata.CompanyName).ToLower().Trim() || tonp.UserName.ToLower().Trim() == Convert.ToString(username + "-" + userdata.Phone).ToLower().Trim())
                              && (string.IsNullOrEmpty(commonsearch)
                                  || (tonp.AadharCard ?? "").ToLower().Contains(commonsearch)
                                  || (tonp.Pancard ?? "").ToLower().Contains(commonsearch)
                                  || (tonp.Gatwaytype ?? "").ToLower().Contains(commonsearch)
                                  || (tonp.OrderId ?? "").ToLower().Contains(commonsearch)
                                  || tonp.ReqBy.ToString().ToLower().Contains(commonsearch)
                                 )

                        select new TxnReportData
                        {
                            Id = tonp.Id,
                            TXN_ID = Convert.ToString(tonp.TxnId),
                            BankRefNo = tonp.OrderId,
                            UserName = tonp.AadharCard,
                            OperatorName = tonp.PanName,
                            AccountNo = tonp.Pancard,
                            OpeningBal = 0,
                            Amount = tonp.Amount,
                            Closing = 0,
                            Status = tonp.Status,
                            APIName = tonp.MobileNo,
                            ComingFrom = Convert.ToString(tonp.Paymentid),
                            MasterDistributor = tum.Name ?? string.Empty,
                            Distributor = tua.Name ?? string.Empty,
                            TimeStamp = tonp.ReqDate,
                            UpdatedTime = tonp.ResDate,
                            Success = string.Empty,
                            Failed = string.Empty,
                            APIRes = ispaginationenabled > 0 ? tonp.Apiresponse ?? string.Empty : "",
                            flagforTrans = 0,
                            CustomerMobile = tonp.Cardno,
                            BeneName = tonp.Cardtype,
                            BRId = tonp.Rrn
                        };
            }
            else if (serviceType == "LESSER REPORT")
            {
                query = from t in _context.Tbluserbalances
                        join tu in _context.TblUsers on t.UserId equals tu.Id
                        where (string.IsNullOrEmpty(dateFrom) || t.Txndate.Value.Date >= DateTime.Parse(dateFrom).Date)
                           && (string.IsNullOrEmpty(dateTo) || t.Txndate.Value.Date <= DateTime.Parse(dateTo).Date)
                           && (userId == 0 || t.UserId == userId)
                            && (string.IsNullOrEmpty(commonsearch)
                                  || (t.Remarks ?? "").ToLower().Contains(commonsearch)
                                  || (t.Amount.ToString() ?? "").ToLower().Contains(commonsearch)
                                  || (t.TxnAmount.ToString() ?? "").ToLower().Contains(commonsearch)
                                  || (t.TxnType ?? "").ToLower().Contains(commonsearch)
                                 )

                        select new TxnReportData
                        {
                            Id = t.Id,
                            TXN_ID = string.Empty,
                            BankRefNo = string.Empty,
                            UserName = tu.Name+"-"+tu.Phone ?? string.Empty,
                            OperatorName = string.Empty,
                            AccountNo = string.Empty,
                            OpeningBal = t.OldBal,
                            Amount = t.Amount,
                            Closing = t.NewBal,
                            Status = t.CrdrType,
                            APIName = t.TxnType,
                            ComingFrom = string.Empty,
                            MasterDistributor = string.Empty,
                            Distributor = string.Empty,
                            TimeStamp = t.Txndate,
                            UpdatedTime = null,
                            Success = string.Empty,
                            Failed = string.Empty,
                            APIRes = t.Remarks ?? string.Empty,
                            flagforTrans = 0,
                            BeneName = Convert.ToString(t.SurCom),
                            CustomerMobile = Convert.ToString(t.Tds),
                            servicename= Convert.ToString(t.TxnAmount)

                        };
            }
            else if (serviceType == "SETTLEMENT")
            {
                flagForTrans = 1;
                query = from tds in _context.SettlementWithdrawals
                        join tud in _context.TblUsers on tds.UserId equals Convert.ToString(tud.Id) into tudJoin
                        from tud in tudJoin.DefaultIfEmpty()

                        where (userId == 0 || tds.UserId == Convert.ToString(userId))
                              && (serviceType == "ALL SERVICE" || serviceType == "SETTLEMENT")
                              && (string.IsNullOrEmpty(dateFrom) || tds.CreatedAt.Date >= DateTime.Parse(dateFrom).Date)
                              && (string.IsNullOrEmpty(dateTo) || tds.CreatedAt.Date <= DateTime.Parse(dateTo).Date)
                              && (string.IsNullOrEmpty(status) || tds.PayoutStatus.Trim().ToUpper() == status)
                              && (string.IsNullOrEmpty(commonsearch)
                                  || (tds.WithdrawalType ?? "").ToLower().Contains(commonsearch)
                                  || (Convert.ToString(tds.Amount) ?? "").ToLower().Contains(commonsearch)
                                  || (tds.BankAccount ?? "").ToLower().Contains(commonsearch)
                                  || (tds.RRN ?? "").ToLower().Contains(commonsearch)
                                  || tds.PayoutTransactionId.ToString().ToLower().Contains(commonsearch)
                                 )

                        select new TxnReportData
                        {
                            Id = tds.Id,
                            TXN_ID = tds.PayoutTransactionId.ToString(),
                            BankRefNo = tds.BankName ?? string.Empty,
                            BRId = tds.RRN ?? string.Empty,
                            UserName = tud.Name + "-" + tud.Phone ?? tud.Username ?? string.Empty,
                            OperatorName = tds.Charge.ToString() ?? "",
                            AccountNo = tds.BankAccount ?? string.Empty,
                            OpeningBal = 0,
                            Amount = tds.Amount,
                            Closing = 0,
                            Status = tds.PayoutStatus ?? string.Empty,
                            APIName = "Settlement" ?? string.Empty,
                            ComingFrom = tds.ComingFrom ?? string.Empty,
                            MasterDistributor = string.Empty,
                            Distributor = string.Empty,
                            TimeStamp = tds.CreatedAt,
                            UpdatedTime = tds.WithdrawalDate,
                            Success = tds.Ifsc ?? "",
                            Failed = string.Empty,
                            APIRes = ispaginationenabled > 0 ? tds.PayoutResponse ?? string.Empty : "",
                            flagforTrans = 1,
                            BeneName = tds.BeneName ?? string.Empty,
                            CustomerMobile = tds.BenePhone ?? string.Empty,
                            servicename = "Settlement" + tds.WithdrawalType ?? string.Empty,
                            Transactionid = tds.PayoutReferenceId ?? string.Empty,
                        };
            }
            else
            {
                flagForTrans = 1;

                query = from tds in _context.TransactionDetails
                        join tum in _context.TblUsers on tds.MdId equals Convert.ToString(tum.Id) into tumJoin
                        from tum in tumJoin.DefaultIfEmpty()

                        join tua in _context.TblUsers on tds.AdId equals Convert.ToString(tua.Id) into tuaJoin
                        from tua in tuaJoin.DefaultIfEmpty()

                        join tud in _context.TblUsers on tds.UserId equals Convert.ToString(tud.Id) into tudJoin
                        from tud in tudJoin.DefaultIfEmpty()

                        where (tds.UserId == Convert.ToString(userId))
                              && (tds.UserName.ToLower().Trim() == username + "-" + userdata.Phone.ToLower().Trim())
                              && (serviceType == "ALL SERVICE" || tds.ServiceName.Trim().ToUpper() == serviceType)
                              && (string.IsNullOrEmpty(dateFrom) || tds.ReqDate.Value.Date >= DateTime.Parse(dateFrom).Date)
                              && (string.IsNullOrEmpty(dateTo) || tds.ReqDate.Value.Date <= DateTime.Parse(dateTo).Date)
                              && (string.IsNullOrEmpty(status) || tds.Status.Trim().ToUpper() == status)
                              && (string.IsNullOrEmpty(commonsearch)
                                  || (tds.AccountNo ?? "").ToLower().Contains(commonsearch)
                                  || (tds.Mobileno ?? "").ToLower().Contains(commonsearch)
                                  || (tds.CustomerName ?? "").ToLower().Contains(commonsearch)
                                  || (tds.Brid ?? "").ToLower().Contains(commonsearch)
                                  || tds.TransId.ToString().ToLower().Contains(commonsearch)
                                 )

                        select new TxnReportData
                        {
                            Id = tds.TransId,
                            TXN_ID = tds.TransId.ToString(),
                            BankRefNo = tds.BankName ?? string.Empty,
                            BRId = tds.Brid ?? string.Empty,
                            UserName = tud.Name+"-"+tud.Phone ?? string.Empty,
                            OperatorName = tds.OperatorName ?? string.Empty,
                            AccountNo = tds.AccountNo ?? string.Empty,
                            OpeningBal = tds.OldBal,
                            Amount = tds.Amount,
                            Closing = Convert.ToDecimal(tds.NewBal),
                            Status = tds.Status ?? string.Empty,
                            APIName = tds.ApiName ?? string.Empty,
                            ComingFrom = tds.ComingFrom ?? string.Empty,
                            MasterDistributor = tum.Name ?? string.Empty,
                            Distributor = tua.Name ?? string.Empty,
                            TimeStamp = tds.ReqDate,
                            UpdatedTime = tds.UpdateDate,
                            Success = string.Empty,
                            Failed = string.Empty,
                            APIRes = ispaginationenabled > 0 ? tds.ApiRes ?? string.Empty : "",
                            flagforTrans = 1,
                            BeneName = tds.CustomerName ?? string.Empty,
                            CustomerMobile = tds.Mobileno ?? string.Empty
                        };
            }

            var baseQuery = query.AsNoTracking();

            // 🔹 Total Count (FULL filtered data)
            var totalTransactions = await baseQuery.CountAsync();

            // 🔹 Total Amount / Balance (FULL filtered data)
            var totalAmount = await baseQuery
                .Select(x => (decimal?)x.Amount)
                .SumAsync() ?? 0;

            var paginated = new List<TxnReportData>();
            if (ispaginationenabled > 0)
            {
                paginated = await query
                .OrderByDescending(x => x.Id)
                .Skip((pageIndex - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
            }
            else
            {
                paginated = await query
                    .OrderByDescending(x => x.Id)
                    .ToListAsync();
            }

            return new PaginatedTxnResultDto
            {
                Data = paginated,
                TotalTransactions = totalTransactions,
                TotalAmount = totalAmount,
                FlagForTrans = flagForTrans
            };
        }

        public async Task<PaginatedTxnResultDto> GetPartnerTransactionReportAsync(
            int partnerId, string userType, string serviceType, string status, string dateFrom, string dateTo,
            int pageIndex = 1, int pageSize = 50, string commonsearch = "", int ispaginationenabled = 1, int filterUserId = 0)
        {
            serviceType = serviceType?.Trim().ToUpper();
            commonsearch = commonsearch?.Trim().ToLower();
            status = status?.Trim().ToUpper();

            var normalizedUserType = userType?.Trim().ToUpper() == "MD" ? "MD" : "AD";
            var partnerIdText = partnerId.ToString();

            // Everyone under this partner's network (their downline), plus the partner's own
            // transactions - mirrors PartnerDashboardService's scoping. Never trusts a
            // client-supplied user id - membership is always resolved from Adid/Mdid here.
            var scopedUserIds = await _context.TblUsers
                .AsNoTracking()
                .Where(u => normalizedUserType == "AD" ? u.Adid == partnerIdText : u.Mdid == partnerIdText)
                .Select(u => u.Id)
                .ToListAsync();
            if (!scopedUserIds.Contains(partnerId))
            {
                scopedUserIds.Add(partnerId);
            }

            if (filterUserId > 0)
            {
                // Narrow to a single downline user - but only if they're actually in this
                // partner's own network. If not, this silently yields zero rows rather than
                // ever leaking another partner's downline data.
                scopedUserIds = scopedUserIds.Where(id => id == filterUserId).ToList();
            }

            var scopedUserIdStrings = scopedUserIds.Select(id => id.ToString()).ToList();

            IQueryable<TxnReportData> query = Enumerable.Empty<TxnReportData>().AsQueryable();
            int flagForTrans = 0;

            if (serviceType == "QR CODE" || serviceType == "ONLINE PAYMENT")
            {
                string gatewayType = serviceType == "QR CODE" ? "UPI" : "Razorpay";

                query = from tonp in _context.Tblonlinepayments
                        join tum in _context.TblUsers on tonp.Mdid equals Convert.ToString(tum.Id) into tumJoin
                        from tum in tumJoin.DefaultIfEmpty()

                        join tua in _context.TblUsers on tonp.AdId equals Convert.ToString(tua.Id) into tuaJoin
                        from tua in tuaJoin.DefaultIfEmpty()

                        join tud in _context.TblUsers on tonp.UserKey equals Convert.ToString(tud.Id) into tudJoin
                        from tud in tudJoin.DefaultIfEmpty()

                        where tonp.Gatwaytype == gatewayType
                              && (string.IsNullOrEmpty(dateFrom) || tonp.ReqDate.Value.Date >= DateTime.Parse(dateFrom).Date)
                              && (string.IsNullOrEmpty(dateTo) || tonp.ReqDate.Value.Date <= DateTime.Parse(dateTo).Date)
                              && (string.IsNullOrEmpty(status) || tonp.Status.ToUpper() == status)
                              && (tonp.UserKey != null && scopedUserIdStrings.Contains(tonp.UserKey))
                              && (string.IsNullOrEmpty(commonsearch)
                                  || (tonp.AadharCard ?? "").ToLower().Contains(commonsearch)
                                  || (tonp.Pancard ?? "").ToLower().Contains(commonsearch)
                                  || (tonp.Gatwaytype ?? "").ToLower().Contains(commonsearch)
                                  || (tonp.OrderId ?? "").ToLower().Contains(commonsearch)
                                  || tonp.ReqBy.ToString().ToLower().Contains(commonsearch)
                                 )

                        select new TxnReportData
                        {
                            Id = tonp.Id,
                            TXN_ID = Convert.ToString(tonp.TxnId),
                            BankRefNo = tonp.OrderId,
                            UserName = (tud.Name ?? string.Empty) + "-" + (tud.Phone ?? string.Empty),
                            OperatorName = tonp.PanName,
                            AccountNo = tonp.Pancard,
                            OpeningBal = 0,
                            Amount = tonp.Amount,
                            Closing = 0,
                            Status = tonp.Status,
                            APIName = tonp.MobileNo,
                            ComingFrom = Convert.ToString(tonp.Paymentid),
                            MasterDistributor = tum.Name ?? string.Empty,
                            Distributor = tua.Name ?? string.Empty,
                            TimeStamp = tonp.ReqDate,
                            UpdatedTime = tonp.ResDate,
                            Success = string.Empty,
                            Failed = string.Empty,
                            APIRes = ispaginationenabled > 0 ? tonp.Apiresponse ?? string.Empty : "",
                            flagforTrans = 0,
                            CustomerMobile = tonp.Cardno,
                            BeneName = tonp.Cardtype,
                            BRId = tonp.Rrn
                        };
            }
            else if (serviceType == "LESSER REPORT")
            {
                query = from t in _context.Tbluserbalances
                        join tu in _context.TblUsers on t.UserId equals tu.Id
                        where t.UserId != null && scopedUserIds.Contains(t.UserId.Value)
                           && (string.IsNullOrEmpty(dateFrom) || t.Txndate.Value.Date >= DateTime.Parse(dateFrom).Date)
                           && (string.IsNullOrEmpty(dateTo) || t.Txndate.Value.Date <= DateTime.Parse(dateTo).Date)
                            && (string.IsNullOrEmpty(commonsearch)
                                  || (t.Remarks ?? "").ToLower().Contains(commonsearch)
                                  || (t.Amount.ToString() ?? "").ToLower().Contains(commonsearch)
                                  || (t.TxnAmount.ToString() ?? "").ToLower().Contains(commonsearch)
                                  || (t.TxnType ?? "").ToLower().Contains(commonsearch)
                                 )

                        select new TxnReportData
                        {
                            Id = t.Id,
                            TXN_ID = string.Empty,
                            BankRefNo = string.Empty,
                            UserName = tu.Name + "-" + tu.Phone ?? string.Empty,
                            OperatorName = string.Empty,
                            AccountNo = string.Empty,
                            OpeningBal = t.OldBal,
                            Amount = t.Amount,
                            Closing = t.NewBal,
                            Status = t.CrdrType,
                            APIName = t.TxnType,
                            ComingFrom = string.Empty,
                            MasterDistributor = string.Empty,
                            Distributor = string.Empty,
                            TimeStamp = t.Txndate,
                            UpdatedTime = null,
                            Success = string.Empty,
                            Failed = string.Empty,
                            APIRes = t.Remarks ?? string.Empty,
                            flagforTrans = 0,
                            BeneName = Convert.ToString(t.SurCom),
                            CustomerMobile = Convert.ToString(t.Tds),
                            servicename = Convert.ToString(t.TxnAmount)
                        };
            }
            else if (serviceType == "SETTLEMENT")
            {
                flagForTrans = 1;
                query = from tds in _context.SettlementWithdrawals
                        join tud in _context.TblUsers on tds.UserId equals Convert.ToString(tud.Id) into tudJoin
                        from tud in tudJoin.DefaultIfEmpty()

                        where tds.UserId != null && scopedUserIdStrings.Contains(tds.UserId)
                              && (string.IsNullOrEmpty(dateFrom) || tds.CreatedAt.Date >= DateTime.Parse(dateFrom).Date)
                              && (string.IsNullOrEmpty(dateTo) || tds.CreatedAt.Date <= DateTime.Parse(dateTo).Date)
                              && (string.IsNullOrEmpty(status) || tds.PayoutStatus.Trim().ToUpper() == status)
                              && (string.IsNullOrEmpty(commonsearch)
                                  || (tds.WithdrawalType ?? "").ToLower().Contains(commonsearch)
                                  || (Convert.ToString(tds.Amount) ?? "").ToLower().Contains(commonsearch)
                                  || (tds.BankAccount ?? "").ToLower().Contains(commonsearch)
                                  || (tds.RRN ?? "").ToLower().Contains(commonsearch)
                                  || tds.PayoutTransactionId.ToString().ToLower().Contains(commonsearch)
                                 )

                        select new TxnReportData
                        {
                            Id = tds.Id,
                            TXN_ID = tds.PayoutTransactionId.ToString(),
                            BankRefNo = tds.BankName ?? string.Empty,
                            BRId = tds.RRN ?? string.Empty,
                            UserName = tud.Name + "-" + tud.Phone ?? tud.Username ?? string.Empty,
                            OperatorName = tds.Charge.ToString() ?? "",
                            AccountNo = tds.BankAccount ?? string.Empty,
                            OpeningBal = 0,
                            Amount = tds.Amount,
                            Closing = 0,
                            Status = tds.PayoutStatus ?? string.Empty,
                            APIName = "Settlement" ?? string.Empty,
                            ComingFrom = tds.ComingFrom ?? string.Empty,
                            MasterDistributor = string.Empty,
                            Distributor = string.Empty,
                            TimeStamp = tds.CreatedAt,
                            UpdatedTime = tds.WithdrawalDate,
                            Success = tds.Ifsc ?? "",
                            Failed = string.Empty,
                            APIRes = ispaginationenabled > 0 ? tds.PayoutResponse ?? string.Empty : "",
                            flagforTrans = 1,
                            BeneName = tds.BeneName ?? string.Empty,
                            CustomerMobile = tds.BenePhone ?? string.Empty,
                            servicename = "Settlement" + tds.WithdrawalType ?? string.Empty,
                            Transactionid = tds.PayoutReferenceId ?? string.Empty,
                        };
            }
            else
            {
                flagForTrans = 1;

                query = from tds in _context.TransactionDetails
                        join tum in _context.TblUsers on tds.MdId equals Convert.ToString(tum.Id) into tumJoin
                        from tum in tumJoin.DefaultIfEmpty()

                        join tua in _context.TblUsers on tds.AdId equals Convert.ToString(tua.Id) into tuaJoin
                        from tua in tuaJoin.DefaultIfEmpty()

                        join tud in _context.TblUsers on tds.UserId equals Convert.ToString(tud.Id) into tudJoin
                        from tud in tudJoin.DefaultIfEmpty()

                        where (tds.UserId != null && scopedUserIdStrings.Contains(tds.UserId))
                              && (serviceType == "ALL SERVICE" || tds.ServiceName.Trim().ToUpper() == serviceType)
                              && (string.IsNullOrEmpty(dateFrom) || tds.ReqDate.Value.Date >= DateTime.Parse(dateFrom).Date)
                              && (string.IsNullOrEmpty(dateTo) || tds.ReqDate.Value.Date <= DateTime.Parse(dateTo).Date)
                              && (string.IsNullOrEmpty(status) || tds.Status.Trim().ToUpper() == status)
                              && (string.IsNullOrEmpty(commonsearch)
                                  || (tds.AccountNo ?? "").ToLower().Contains(commonsearch)
                                  || (tds.Mobileno ?? "").ToLower().Contains(commonsearch)
                                  || (tds.CustomerName ?? "").ToLower().Contains(commonsearch)
                                  || (tds.Brid ?? "").ToLower().Contains(commonsearch)
                                  || tds.TransId.ToString().ToLower().Contains(commonsearch)
                                 )

                        select new TxnReportData
                        {
                            Id = tds.TransId,
                            TXN_ID = tds.TransId.ToString(),
                            BankRefNo = tds.BankName ?? string.Empty,
                            BRId = tds.Brid ?? string.Empty,
                            UserName = tud.Name + "-" + tud.Phone ?? string.Empty,
                            OperatorName = tds.OperatorName ?? string.Empty,
                            AccountNo = tds.AccountNo ?? string.Empty,
                            OpeningBal = tds.OldBal,
                            Amount = tds.Amount,
                            Closing = Convert.ToDecimal(tds.NewBal),
                            Status = tds.Status ?? string.Empty,
                            APIName = tds.ApiName ?? string.Empty,
                            ComingFrom = tds.ComingFrom ?? string.Empty,
                            MasterDistributor = tum.Name ?? string.Empty,
                            Distributor = tua.Name ?? string.Empty,
                            TimeStamp = tds.ReqDate,
                            UpdatedTime = tds.UpdateDate,
                            Success = string.Empty,
                            Failed = string.Empty,
                            APIRes = ispaginationenabled > 0 ? tds.ApiRes ?? string.Empty : "",
                            flagforTrans = 1,
                            BeneName = tds.CustomerName ?? string.Empty,
                            CustomerMobile = tds.Mobileno ?? string.Empty
                        };
            }

            var baseQuery = query.AsNoTracking();

            var totalTransactions = await baseQuery.CountAsync();

            var totalAmount = await baseQuery
                .Select(x => (decimal?)x.Amount)
                .SumAsync() ?? 0;

            var paginated = new List<TxnReportData>();
            if (ispaginationenabled > 0)
            {
                paginated = await query
                    .OrderByDescending(x => x.Id)
                    .Skip((pageIndex - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();
            }
            else
            {
                paginated = await query
                    .OrderByDescending(x => x.Id)
                    .ToListAsync();
            }

            return new PaginatedTxnResultDto
            {
                Data = paginated,
                TotalTransactions = totalTransactions,
                TotalAmount = totalAmount,
                FlagForTrans = flagForTrans
            };
        }

        public async Task<List<PartnerUserDropdownDto>> GetPartnerUserDropdownAsync(int partnerId, string userType)
        {
            var normalizedUserType = userType?.Trim().ToUpper() == "MD" ? "MD" : "AD";
            var partnerIdText = partnerId.ToString();

            var users = await _context.TblUsers
                .AsNoTracking()
                .Where(u => normalizedUserType == "AD" ? u.Adid == partnerIdText : u.Mdid == partnerIdText)
                .OrderBy(u => u.Name)
                .Select(u => new PartnerUserDropdownDto
                {
                    Id = u.Id,
                    Label = (u.Name ?? u.Username ?? "User") + " - " + (u.Phone ?? string.Empty)
                })
                .ToListAsync();

            return users;
        }

    }
}

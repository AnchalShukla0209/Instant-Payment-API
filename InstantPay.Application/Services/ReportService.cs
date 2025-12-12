using InstantPay.Application.Interfaces;
using InstantPay.Infrastructure.Sql.Entities;
using InstantPay.SharedKernel.Entity;
using Microsoft.EntityFrameworkCore;
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
        public ReportService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<PaginatedTxnResultDto> GetTransactionReportAsync(
    string serviceType, string status, string dateFrom, string dateTo,
    int userId, int pageIndex = 1, int pageSize = 50)
        {
            serviceType = serviceType?.Trim().ToUpper();
            status = status?.Trim().ToUpper();

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
                              && (userId == 0 || tonp.UserKey == Convert.ToString(userId))

                        select new TxnReportData
                        {
                            Id = tonp.Id,
                            TXN_ID = Convert.ToString(tonp.TxnId),
                            BankRefNo = string.Empty,
                            UserName = tud.Username ?? string.Empty,
                            OperatorName = string.Empty,
                            AccountNo = string.Empty,
                            OpeningBal = 0,
                            Amount = tonp.Amount,
                            Closing = 0,
                            Status = tonp.Status,
                            APIName = string.Empty,
                            ComingFrom = string.Empty,
                            MasterDistributor = tum.Name ?? string.Empty,
                            Distributor = tua.Name ?? string.Empty,
                            TimeStamp = tonp.ReqDate,
                            UpdatedTime = null,
                            Success = string.Empty,
                            Failed = string.Empty,
                            APIRes = tonp.Apiresponse ?? string.Empty,
                            flagforTrans = 0
                        };
            }
            else if (serviceType == "LESSER REPORT")
            {
                query = from t in _context.Tbluserbalances
                        where (string.IsNullOrEmpty(dateFrom) || t.Txndate.Value.Date >= DateTime.Parse(dateFrom).Date)
                           && (string.IsNullOrEmpty(dateTo) || t.Txndate.Value.Date <= DateTime.Parse(dateTo).Date)
                           && (userId == 0 || t.UserId == userId)

                        select new TxnReportData
                        {
                            Id = t.Id,
                            TXN_ID = string.Empty,
                            BankRefNo = string.Empty,
                            UserName = t.UserName ?? string.Empty,
                            OperatorName = string.Empty,
                            AccountNo = string.Empty,
                            OpeningBal = t.OldBal,
                            Amount = t.Amount,
                            Closing = t.NewBal,
                            Status = t.CrdrType,
                            APIName = string.Empty,
                            ComingFrom = string.Empty,
                            MasterDistributor = string.Empty,
                            Distributor = string.Empty,
                            TimeStamp = t.Txndate,
                            UpdatedTime = null,
                            Success = string.Empty,
                            Failed = string.Empty,
                            APIRes = t.Remarks ?? string.Empty,
                            flagforTrans = 0
                        };
            }
            else if(serviceType == "ADMIN LESSER REPORT")
            {
                query = from t in _context.TblWlbalances
                        where (string.IsNullOrEmpty(dateFrom) || t.Txndate.Value.Date >= DateTime.Parse(dateFrom).Date)
                           && (string.IsNullOrEmpty(dateTo) || t.Txndate.Value.Date <= DateTime.Parse(dateTo).Date)
                           && (userId == 0 || t.UserId == Convert.ToString(userId))

                        select new TxnReportData
                        {
                            Id = t.Id,
                            TXN_ID = string.Empty,
                            BankRefNo = string.Empty,
                            UserName = t.UserName ?? string.Empty,
                            OperatorName = string.Empty,
                            AccountNo = string.Empty,
                            OpeningBal = t.OldBal,
                            Amount = t.Amount,
                            Closing = t.NewBal,
                            Status = t.CrdrType,
                            APIName = string.Empty,
                            ComingFrom = string.Empty,
                            MasterDistributor = string.Empty,
                            Distributor = string.Empty,
                            TimeStamp = t.Txndate,
                            UpdatedTime = null,
                            Success = string.Empty,
                            Failed = string.Empty,
                            APIRes = t.Remarks ?? string.Empty,
                            flagforTrans = 0
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

                        select new TxnReportData
                        {
                            Id = tds.TransId,
                            TXN_ID = tds.TransId.ToString(),
                            BankRefNo = tds.BankName ?? string.Empty,
                            UserName = tud.Username ?? string.Empty,
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
                            APIRes = tds.ApiRes ?? string.Empty,
                            flagforTrans = 1
                        };
            }
            var totalTransactions = await query.CountAsync();
            var totalAmount = await query.SumAsync(x => (decimal?)x.Amount) ?? 0;

            var paginated = await query
                .OrderByDescending(x => x.Id)
                .Skip((pageIndex - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return new PaginatedTxnResultDto
            {
                Data = paginated,
                TotalTransactions = totalTransactions,
                TotalAmount = totalAmount,
                FlagForTrans = flagForTrans
            };
        }


        public async Task<TxnDetailsData> GetTxnDetails(int txnId)
        {
            var txn = await (from t in _context.TransactionDetails
                             where t.TransId == txnId
                             select new TxnDetailsData
                             {
                                 TransId = t.TransId,
                                 Status = t.Status ?? string.Empty,
                                 AccountNo = t.AccountNo ?? string.Empty,
                                 Cost = t.Cost,
                                 ServiceName = t.ServiceName ?? string.Empty,
                                 UserName = t.UserName ?? string.Empty,
                                 UserId = t.UserId ?? string.Empty
                             }).FirstOrDefaultAsync();

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

                var txn = await _context.TransactionDetails.FirstOrDefaultAsync(t => t.TransId == request.TransId);
                if (txn == null)
                {
                    return (new TxnUpdateResponse { ErrorMsg = "Transaction not found", Flag = false });
                }

                if (!string.Equals(request.Status?.Trim(), "REFUND", StringComparison.OrdinalIgnoreCase))
                {
                    txn.Status = request.Status;
                    txn.AdminRemarks = request.Remarks;
                    txn.UpdateDate = DateTime.UtcNow;
                    _context.TransactionDetails.Update(txn);
                    await _context.SaveChangesAsync();
                }

                if (string.Equals(request.Status?.Trim(), "REFUND", StringComparison.OrdinalIgnoreCase))
                {
                    bool isPinValid = string.Equals(request.TxnPin?.Trim(), admin.Refundpin?.Trim(), StringComparison.OrdinalIgnoreCase)
                             || string.Equals(request.Status?.Trim(), "SUCCESS", StringComparison.OrdinalIgnoreCase);

                    if (!isPinValid)
                    {
                        return (new TxnUpdateResponse { ErrorMsg = "Invalid Refund Pin", Flag = false });
                    }

                    var user = await _context.TblUsers.FirstOrDefaultAsync(u => u.Id == request.UserId);
                    if (user != null)
                    {
                        var lastBalance = await _context.Tbluserbalances
                            .Where(b => b.UserId == request.UserId)
                            .OrderByDescending(b => b.Id)
                            .Select(b => b.NewBal)
                            .FirstOrDefaultAsync();

                        decimal oldBal = (decimal)lastBalance;
                        decimal newBal = oldBal + request.Amount;

                        var walletTxn = new Tbluserbalance
                        {
                            TxnAmount = request.TxnAmount,
                            SurCom = 0,
                            Tds = 0,
                            UserId = request.UserId,
                            UserName = request.UserName,
                            OldBal = oldBal,
                            Amount = request.Amount,
                            NewBal = newBal,
                            TxnType = request.ServiceName,
                            CrdrType = "Credit",
                            Remarks = $"Reverse Amount Credit For {request.ServiceName}",
                            WlId = user.Wlid,
                            Txndate = DateTime.UtcNow
                        };

                        await _context.Tbluserbalances.AddAsync(walletTxn);
                        await _context.SaveChangesAsync();
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
   int userId, string username, int pageIndex = 1, int pageSize = 50)
        {
            serviceType = serviceType?.Trim().ToUpper();
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
                              && (tonp.UserName.ToLower().Trim() == Convert.ToString(username+"-"+userdata.Phone).ToLower().Trim())

                        select new TxnReportData
                        {
                            Id = tonp.Id,
                            TXN_ID = Convert.ToString(tonp.TxnId),
                            BankRefNo = string.Empty,
                            UserName = tud.Username ?? string.Empty,
                            OperatorName = string.Empty,
                            AccountNo = string.Empty,
                            OpeningBal = 0,
                            Amount = tonp.Amount,
                            Closing = 0,
                            Status = tonp.Status,
                            APIName = string.Empty,
                            ComingFrom = string.Empty,
                            MasterDistributor = tum.Name ?? string.Empty,
                            Distributor = tua.Name ?? string.Empty,
                            TimeStamp = tonp.ReqDate,
                            UpdatedTime = null,
                            Success = string.Empty,
                            Failed = string.Empty,
                            APIRes = tonp.Apiresponse ?? string.Empty,
                            flagforTrans = 0
                        };
            }
            else if (serviceType == "LESSER REPORT")
            {
                query = from t in _context.Tbluserbalances
                        where (string.IsNullOrEmpty(dateFrom) || t.Txndate.Value.Date >= DateTime.Parse(dateFrom).Date)
                           && (string.IsNullOrEmpty(dateTo) || t.Txndate.Value.Date <= DateTime.Parse(dateTo).Date)
                           && (userId == 0 || t.UserId == userId)

                        select new TxnReportData
                        {
                            Id = t.Id,
                            TXN_ID = string.Empty,
                            BankRefNo = string.Empty,
                            UserName = t.UserName ?? string.Empty,
                            OperatorName = string.Empty,
                            AccountNo = string.Empty,
                            OpeningBal = t.OldBal,
                            Amount = t.Amount,
                            Closing = t.NewBal,
                            Status = t.CrdrType,
                            APIName = string.Empty,
                            ComingFrom = string.Empty,
                            MasterDistributor = string.Empty,
                            Distributor = string.Empty,
                            TimeStamp = t.Txndate,
                            UpdatedTime = null,
                            Success = string.Empty,
                            Failed = string.Empty,
                            APIRes = t.Remarks ?? string.Empty,
                            flagforTrans = 0
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

                        where ( tds.UserId == Convert.ToString(userId))
                              && (tds.UserName.ToLower().Trim()==username+"-"+userdata.Phone.ToLower().Trim())
                              && (serviceType == "ALL SERVICE" || tds.ServiceName.Trim().ToUpper() == serviceType)
                              && (string.IsNullOrEmpty(dateFrom) || tds.ReqDate.Value.Date >= DateTime.Parse(dateFrom).Date)
                              && (string.IsNullOrEmpty(dateTo) || tds.ReqDate.Value.Date <= DateTime.Parse(dateTo).Date)
                              && (string.IsNullOrEmpty(status) || tds.Status.Trim().ToUpper() == status)

                        select new TxnReportData
                        {
                            Id = tds.TransId,
                            TXN_ID = tds.TransId.ToString(),
                            BankRefNo = tds.BankName ?? string.Empty,
                            BRId = tds.Brid ?? string.Empty,
                            UserName = tud.Username ?? string.Empty,
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
                            APIRes = tds.ApiRes ?? string.Empty,
                            flagforTrans = 1
                        };
            }
            var totalTransactions = await query.CountAsync();
            var totalAmount = await query.SumAsync(x => (decimal?)x.Amount) ?? 0;

            var paginated = await query
                .OrderByDescending(x => x.Id)
                .Skip((pageIndex - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return new PaginatedTxnResultDto
            {
                Data = paginated,
                TotalTransactions = totalTransactions,
                TotalAmount = totalAmount,
                FlagForTrans = flagForTrans
            };
        }






    }
}

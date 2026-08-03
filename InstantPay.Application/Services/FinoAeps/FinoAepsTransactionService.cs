using InstantPay.Application.Interfaces;
using InstantPay.Application.Interfaces.FinoAeps;
using InstantPay.Infrastructure.Sql.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;

namespace InstantPay.Application.Services.FinoAeps
{
    public class FinoAepsTransactionService : IFinoAepsTransactionService
    {
        private readonly AppDbContext _context;
        private readonly ILogger<FinoAepsTransactionService> _logger;
        private readonly IWalletService _walletService;

        public FinoAepsTransactionService(AppDbContext context, ILogger<FinoAepsTransactionService> logger, IWalletService walletService)
        {
            _context = context;
            _logger = logger;
            _walletService = walletService;
        }

        public async Task<bool> InsertPendingAsync(FinoAepsTxnRecord r, CancellationToken ct = default)
        {
            try
            {
                var user = await _context.TblUsers
                    .Where(u => u.Id == r.UserId)
                    .Select(u => new { u.Name, u.Phone, u.Wlid, u.Mdid, u.Adid })
                    .FirstOrDefaultAsync(ct);

                decimal oldBal = await _walletService.GetBalanceAsync(r.UserId, ct);

                _context.TransactionDetails.Add(new TransactionDetail
                {
                    UserId = r.UserId.ToString(),
                    UserName = $"{user?.Name}-{user?.Phone}",
                    WlId = user?.Wlid?.ToString(),
                    MdId = user?.Mdid?.ToString(),
                    AdId = user?.Adid?.ToString(),
                    TxnId = r.TxnId,
                    ServiceName = "AEPS",
                    OperatorName = r.OperatorName,
                    OpId = r.OpId,
                    TxnType = r.TxnType,
                    Mobileno = r.Mobile,
                    OldBal = oldBal,
                    Amount = r.Amount,
                    Comm = r.Comm,
                    MdComm = r.MdComm,
                    AdComm = r.AdComm,
                    WlComm = r.WlComm,
                    Tds = r.Tds,
                    Cost = r.Cost,
                    NewBal = Convert.ToString(r.NewBal > 0 ? r.NewBal : oldBal),
                    Charge = r?.charge ?? 0,
                    Status = "Pending",
                    ApiTxnId = r.TxnId,
                    ApiName = "FINO",
                    ApiMsg = "",
                    ApiRes = "",
                    ApiReq = Truncate(r.ApiReq, 4000),
                    AccountNo = r.AadhaarNo,
                    BankName = r.BankName,
                    IfscCode = r.IfscCode,
                    CustomerName = r.CustomerName,
                    ComingFrom = r.ComingFrom,
                    Brid = "",
                    ReqDate = DateTime.Now,
                    UpdateDate = DateTime.Now
                });

                await _context.SaveChangesAsync(ct);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "FinoAepsTransactionService.InsertPendingAsync failed for TxnId={TxnId}", r.TxnId);
                return false;
            }
        }

        public async Task InsertAsync(FinoAepsTxnRecord r, CancellationToken ct = default)
        {
            try
            {
                var user = await _context.TblUsers
                    .Where(u => u.Id == r.UserId)
                    .Select(u => new { u.Name, u.Phone, u.Wlid, u.Mdid, u.Adid })
                    .FirstOrDefaultAsync(ct);

                decimal oldBal = await _walletService.GetBalanceAsync(r.UserId, ct);

                _context.TransactionDetails.Add(new TransactionDetail
                {
                    UserId = r.UserId.ToString(),
                    UserName = $"{user?.Name}-{user?.Phone}",
                    WlId = user?.Wlid?.ToString(),
                    MdId = user?.Mdid?.ToString(),
                    AdId = user?.Adid?.ToString(),
                    TxnId = r.TxnId,
                    ServiceName = "AEPS",
                    OperatorName = "FINO",
                    OpId = r.OpId,
                    TxnType = r.TxnType,
                    Mobileno = r.Mobile,
                    OldBal = oldBal,
                    Amount = r.Amount,
                    Comm = r.Comm,
                    MdComm = r.MdComm,
                    AdComm = r.AdComm,
                    WlComm = r.WlComm,
                    Tds = r.Tds,
                    Cost = r.Cost,
                    NewBal = Convert.ToString(r.NewBal > 0 ? r.NewBal : oldBal),
                    Charge = 0,
                    Status = r.Status,
                    ApiTxnId = r.Rrn,
                    ApiName = "FINO",
                    ApiMsg = Truncate(r.ApiMsg, 500),
                    ApiRes = Truncate(r.ApiRes, 4000),
                    ApiReq = Truncate(r.ApiReq, 4000),
                    AccountNo = r.AadhaarNo,
                    BankName = r.BankName,
                    IfscCode = r.IfscCode,
                    CustomerName = r.CustomerName,
                    ComingFrom = "APP",
                    Brid = r.Brid,
                    ReqDate = DateTime.Now,
                    UpdateDate = DateTime.Now
                });

                await _context.SaveChangesAsync(ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "FinoAepsTransactionService.InsertAsync failed for TxnId={TxnId}", r.TxnId);
            }
        }

        public async Task UpdateStatusAsync(string txnId, string status, string? apiRes, string? rrn, CancellationToken ct = default)
        {
            try
            {
                var txn = await _context.TransactionDetails
                    .FirstOrDefaultAsync(t => t.TxnId == txnId, ct);

                if (txn == null) return;

                txn.Status = status;
                txn.UpdateDate = DateTime.Now;
                txn.Brid = rrn;
                if (status.Equals("FAILED", StringComparison.OrdinalIgnoreCase))
                    txn.NewBal = Convert.ToString(txn.OldBal);
                if (apiRes != null) txn.ApiRes = Truncate(apiRes, 4000);

                await _context.SaveChangesAsync(ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "FinoAepsTransactionService.UpdateStatusAsync failed for TxnId={TxnId}", txnId);
            }
        }

        public async Task UpdateWithCommissionAsync(string txnId, string status, string? apiRes, FinoAepsCommission commission, decimal newBal, string RRN, bool onUs = false, decimal requestedamount=0 , CancellationToken ct = default)
        {
            try
            {
                var txn = await _context.TransactionDetails
                    .FirstOrDefaultAsync(t => t.TxnId == txnId, ct);

                if (txn == null) return;

                txn.Status = status;
                txn.UpdateDate = DateTime.Now;
                if (apiRes != null) txn.ApiRes = Truncate(apiRes, 4000);
                txn.Comm = onUs ? 0: commission?.RetailerCommission ?? 0;
                txn.MdComm = onUs ? 0: commission?.MdCommission ?? 0;
                txn.AdComm = onUs ? 0 : commission?.AdCommission ?? 0;
                txn.WlComm = onUs ? 0 : commission?.WlCommission ?? 0;
                txn.Tds = onUs ? 0 : commission?.Tds ?? 0;
                txn.Cost = onUs ? requestedamount : commission?.Cost ?? 0;
                txn.NewBal = status.Equals("FAILED", StringComparison.OrdinalIgnoreCase)
                    ? Convert.ToString(txn.OldBal)
                    : Convert.ToString(newBal);
                txn.Brid = RRN;
                await _context.SaveChangesAsync(ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "FinoAepsTransactionService.UpdateWithCommissionAsync failed for TxnId={TxnId}", txnId);
            }
        }

        private static string? Truncate(string? s, int max)
            => s == null ? null : s.Length <= max ? s : s[..max];
    }
}

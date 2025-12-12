using InstantPay.Application.Interfaces;
using InstantPay.Infrastructure.Sql.Entities; // Your EF entities
using InstantPay.SharedKernel.Entity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

public class PaymentService : IPaymentService
{
    private readonly AppDbContext _context;
    private readonly ILogger<PaymentService> _logger;
    private readonly string _basePath = Path.Combine(Directory.GetCurrentDirectory(), "UploadFiles", "PaymentRequestTxn");

    public PaymentService(AppDbContext context, ILogger<PaymentService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<Guid> CreatePaymentRequestAsync(PaymentRequestDto request, int userId)
    {
        try
        {
            if (request.Amount <= 0) throw new ArgumentException("Invalid amount");
            if (string.IsNullOrWhiteSpace(request.TxnId)) throw new ArgumentException("TxnId is mandatory");
            if (request.TxnSlip == null) throw new ArgumentException("Txn slip file is mandatory");

            var ext = Path.GetExtension(request.TxnSlip.FileName).ToLower();
            if (ext != ".jpg" && ext != ".png") throw new ArgumentException("Only jpg and png allowed");

            var payment = new TblPaymentRequest
            {
                PaymentId = Guid.NewGuid(),
                BankId = request.BankId,
                UserId = userId,
                Amount = request.Amount,
                TxnId = request.TxnId,
                DeposideMode = request.DeposideMode,
                Status = "Pending",
                CreatedBy = userId,
                CreatedOn = DateTime.UtcNow,
                IsDeleted = false
            };


            string webRootPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
            string basePath = Path.Combine(webRootPath, "UploadFiles", "PaymentRequestTxn", payment.PaymentId.ToString());
            if (!Directory.Exists(basePath)) Directory.CreateDirectory(basePath);

            string filePath = Path.Combine(basePath, request.TxnSlip.FileName);
            if (File.Exists(filePath)) throw new IOException("Duplicate file exists");


            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await request.TxnSlip.CopyToAsync(stream);
            }

            payment.TxnSlipFileName = request.TxnSlip.FileName;
            string TxnSlip = Path.Combine("UploadFiles", "PaymentRequestTxn", payment.PaymentId.ToString(), request.TxnSlip.FileName).Replace("\\", "/");
            payment.TxnSlipPath = TxnSlip;

            await _context.TblPaymentRequest.AddAsync(payment);
            await _context.SaveChangesAsync();

            return payment.PaymentId;
        }
        catch (Exception ex)
        {
            throw ex;
        }
    }

    public async Task UpdatePaymentAsync(PaymentUpdateDto request)
    {
        using var trx = await _context.Database.BeginTransactionAsync();
        try
        {
            var payment = await _context.TblPaymentRequest.FirstOrDefaultAsync(p => p.PaymentId == request.PaymentId);
            if (payment == null) throw new KeyNotFoundException("Payment not found");

            payment.Status = request.Status;
            payment.AdminRemarks = request.AdminRemarks;
            payment.ModifiedBy = request.ModifiedBy;
            payment.ModifiedOn = DateTime.UtcNow;

            if (request.Status == "Approved")
            {
                var user = await _context.TblUsers.FirstOrDefaultAsync(u => u.Id == payment.UserId);
                if (user != null)
                {
                    var lastBalance = await _context.Tbluserbalances
                        .Where(b => b.UserId == payment.UserId)
                        .OrderByDescending(b => b.Id)
                        .Select(b => b.NewBal)
                        .FirstOrDefaultAsync();

                    decimal oldBal = (decimal)lastBalance;
                    decimal newBal = oldBal + payment.Amount;

                    var walletTxn = new Tbluserbalance
                    {
                        TxnAmount = payment.Amount,
                        SurCom = 0,
                        Tds = 0,
                        UserId = user.Id,
                        UserName = user.Username,
                        OldBal = oldBal,
                        Amount = payment.Amount,
                        NewBal = newBal,
                        TxnType = "PaymentApproval",
                        CrdrType = "Credit",
                        Remarks = $"Payment approved for Txn {payment.TxnId}",
                        WlId = user.Wlid,
                        Txndate = DateTime.UtcNow
                    };

                    await _context.Tbluserbalances.AddAsync(walletTxn);
                }
            }
            else if (request.Status == "Rejected")
            {
                if (string.IsNullOrWhiteSpace(request.AdminRemarks))
                    throw new ArgumentException("Admin remarks mandatory when rejecting");
            }

            await _context.SaveChangesAsync();
            await trx.CommitAsync();
        }
        catch
        {
            await trx.RollbackAsync();
            throw;
        }
    }

    public async Task<(IEnumerable<PaymentResponseDto> Payments, int TotalCount)>
    GetAllPaymentsAsync(int pageNumber, int pageSize, string status, DateTime? fromDate, DateTime? toDate)
    {
        try
        {
            // Start with the base query
            var query = _context.TblPaymentRequest
                .Where(p => !p.IsDeleted);

            // Apply status filter
            if (!string.IsNullOrWhiteSpace(status))
                query = query.Where(p => p.Status == status);

            // Apply date filters (only date part)
            if (fromDate.HasValue)
            {
                var from = fromDate.Value.Date;
                query = query.Where(p => p.CreatedOn >= from);
            }

            if (toDate.HasValue)
            {
                var to = toDate.Value.Date;
                query = query.Where(p => p.CreatedOn <= to);
            }

            // Get total count before pagination
            int totalCount = await query.CountAsync();

            // Join related tables and select DTO after filtering
            var data = await query
                .OrderByDescending(p => p.CreatedOn) // optional: latest first
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .Join(_context.BankMaster,
                      p => p.BankId,
                      b => b.BankId,
                      (p, b) => new { p, b })
                .Join(_context.TblUsers,
                      pb => pb.p.UserId,
                      u => u.Id,
                      (pb, u) => new PaymentResponseDto
                      {
                          PaymentId = pb.p.PaymentId,
                          TxnId = pb.p.TxnId,
                          UserName = u.Username,
                          UserType = u.Usertype,
                          BankName = pb.b.BankName,
                          AccountNo = pb.b.AccountNumber,
                          Amount = pb.p.Amount,
                          DepositeMode = pb.p.DeposideMode,
                          TxnSlipFileName = pb.p.TxnSlipFileName,
                          TxnSlipPath = pb.p.TxnSlipPath,
                          Status = pb.p.Status,
                          AdminRemarks = pb.p.AdminRemarks
                      })
                .ToListAsync();

            return (data, totalCount);
        }
        catch (Exception ex)
        {
            throw;
        }
    }


    public async Task<(byte[] FileContent, string FileName, string ContentType)> DownloadTxnSlipAsync(Guid paymentId)
    {
        try
        {
            var payment = await _context.TblPaymentRequest.FirstOrDefaultAsync(p => p.PaymentId == paymentId);
            if (payment == null || string.IsNullOrEmpty(payment.TxnSlipPath)) throw new FileNotFoundException();

            string webRootPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
            string basePath = Path.Combine(webRootPath, payment.TxnSlipPath);
            var bytes = await File.ReadAllBytesAsync(basePath);
            var contentType = "application/octet-stream";
            return (bytes, payment.TxnSlipFileName, contentType);
        }
        catch(Exception ex)
        {
            throw ex;
        }
    }

    public async Task<PaymentResponseDto> GetPaymentByIdAsync(Guid paymentId)
    {
        try
        {
            var data = await _context.TblPaymentRequest
                .Where(p => !p.IsDeleted && p.PaymentId == paymentId)
                .Join(_context.BankMaster,
                      p => p.BankId,
                      b => b.BankId,
                      (p, b) => new { p, b })
                .Join(_context.TblUsers,
                      pb => pb.p.UserId,
                      u => u.Id,
                      (pb, u) => new PaymentResponseDto
                      {
                          PaymentId = pb.p.PaymentId,
                          TxnId = pb.p.TxnId,
                          UserName = u.Username,
                          UserType = u.Usertype,
                          BankName = pb.b.BankName,
                          AccountNo = pb.b.AccountNumber,
                          Amount = pb.p.Amount,
                          DepositeMode = pb.p.DeposideMode,
                          TxnSlipFileName = pb.p.TxnSlipFileName,
                          TxnSlipPath = pb.p.TxnSlipPath,
                          Status = pb.p.Status,
                          AdminRemarks = pb.p.AdminRemarks
                      })
                .FirstOrDefaultAsync();

            return data;
        }
        catch (Exception ex)
        {
            throw; 
        }
    }

}

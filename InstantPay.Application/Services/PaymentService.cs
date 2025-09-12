using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using InstantPay.Application.Interfaces;
using InstantPay.SharedKernel.Entity;
using InstantPay.Infrastructure.Sql.Entities; // Your EF entities

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
        if (request.Amount <= 0) throw new ArgumentException("Invalid amount");
        if (string.IsNullOrWhiteSpace(request.TxnId)) throw new ArgumentException("TxnId is mandatory");
        if (request.TxnSlip == null) throw new ArgumentException("Txn slip file is mandatory");

        var ext = Path.GetExtension(request.TxnSlip.FileName).ToLower();
        if (ext != ".jpg" && ext != ".png") throw new ArgumentException("Only jpg and png allowed");

        var payment = new TblPaymentRequest
        {
            PaymentId = Guid.NewGuid(),
            BankId = request.BankId,
            UserId = request.UserId,
            Amount = request.Amount,
            TxnId = request.TxnId,
            DeposideMode = request.DeposideMode,
            Status = "Pending",
            CreatedBy = userId,
            CreatedOn = DateTime.UtcNow,
            IsDeleted = false
        };

        string folderPath = Path.Combine(_basePath, payment.PaymentId.ToString());
        if (!Directory.Exists(folderPath)) Directory.CreateDirectory(folderPath);

        string filePath = Path.Combine(folderPath, request.TxnSlip.FileName);
        if (File.Exists(filePath)) throw new IOException("Duplicate file exists");

        using (var stream = new FileStream(filePath, FileMode.Create))
        {
            await request.TxnSlip.CopyToAsync(stream);
        }

        payment.TxnSlipFileName = request.TxnSlip.FileName;
        payment.TxnSlipPath = filePath;

        await _context.TblPaymentRequest.AddAsync(payment);
        await _context.SaveChangesAsync();

        return payment.PaymentId;
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
        var query = from p in _context.TblPaymentRequest
                    join b in _context.BankMaster on p.BankId equals b.BankId
                    join u in _context.TblUsers on p.UserId equals u.Id
                    where !p.IsDeleted
                    select new PaymentResponseDto
                    {
                        PaymentId = p.PaymentId,
                        TxnId = p.TxnId,
                        UserName = u.Username,
                        UserType = u.Usertype,
                        BankName = b.BankName,
                        AccountNo = b.AccountNumber,
                        Amount = p.Amount,
                        DepositeMode = p.DeposideMode,
                        TxnSlipFileName = p.TxnSlipFileName,
                        TxnSlipPath = p.TxnSlipPath,
                        Status = p.Status,
                        AdminRemarks = p.AdminRemarks
                    };

        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(x => x.Status == status);

        if (fromDate.HasValue && toDate.HasValue)
            query = query.Where(x => x.PaymentId != Guid.Empty &&
                                     x.PaymentId != Guid.Empty && // dummy to keep EF happy
                                     x.PaymentId != Guid.Empty &&
                                     x.PaymentId != Guid.Empty);

        int totalCount = await query.CountAsync();
        var data = await query.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToListAsync();

        return (data, totalCount);
    }

    public async Task<(byte[] FileContent, string FileName, string ContentType)> DownloadTxnSlipAsync(Guid paymentId)
    {
        var payment = await _context.TblPaymentRequest.FirstOrDefaultAsync(p => p.PaymentId == paymentId);
        if (payment == null || string.IsNullOrEmpty(payment.TxnSlipPath)) throw new FileNotFoundException();

        var bytes = await File.ReadAllBytesAsync(payment.TxnSlipPath);
        var contentType = "application/octet-stream";
        return (bytes, payment.TxnSlipFileName, contentType);
    }
}

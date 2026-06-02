using InstantPay.Application.Interfaces;
using InstantPay.Application.Interfaces.SMS;
using InstantPay.Application.Services.SMS;
using InstantPay.Infrastructure.Sql.Entities; // Your EF entities
using InstantPay.SharedKernel.Entity;
using InstantPay.SharedKernel.RequestPayload.DebitCredit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using static MongoDB.Driver.WriteConcern;

namespace InstantPay.Application.Services
{
    public class PaymentService : IPaymentService
    {
        private readonly AppDbContext _context;
        private readonly ILogger<PaymentService> _logger;
        private readonly ISmsService _smsService;
        private readonly string _basePath = Path.Combine(Directory.GetCurrentDirectory(), "UploadFiles", "PaymentRequestTxn");

        public PaymentService(AppDbContext context, ILogger<PaymentService> logger, ISmsService smsService)
        {
            _context = context;
            _logger = logger;
            _smsService = smsService;
        }

        public async Task<Guid> CreatePaymentRequestAsync(PaymentRequestDto request, int userId)
        {
            try
            {
                if (request.Amount <= 0) throw new ArgumentException("Invalid amount");
                if (string.IsNullOrWhiteSpace(request.PaymentTxnId)) throw new ArgumentException("TxnId is mandatory");
                if (request.TxnSlip == null) throw new ArgumentException("Txn slip file is mandatory");

                var ext = Path.GetExtension(request.TxnSlip.FileName).ToLower();
                if (ext != ".jpg" && ext != ".png") throw new ArgumentException("Only jpg and png allowed");

                var payment = new TblPaymentRequest
                {
                    TxnId = "0",
                    PaymentId = Guid.NewGuid(),
                    BankId = request.BankId,
                    UserId = userId,
                    Amount = request.Amount,
                    DeposideMode = request.DeposideMode,
                    Status = "Pending",
                    CreatedBy = userId,
                    CreatedOn = DateTime.Now,
                    IsDeleted = false,
                    UserRemarks = request.UserRemarks,
                    openingBalance = 0,
                    closingBalance = 0,
                    PaymentTxnId = request.PaymentTxnId
                };

                var isDuplicatePaymentTxnId = _context.TblPaymentRequest.Where(id => id.PaymentTxnId.Trim().ToLower() == request.PaymentTxnId.Trim().ToLower()).FirstOrDefault();
                if (isDuplicatePaymentTxnId != null)
                {
                    throw new ArgumentException("Request already submitted for this Transaction Id, Please use anther Transaction Id.");
                }

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

                return (Guid)payment.PaymentId;
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        public async Task UpdatePaymentAsync(PaymentUpdateDto request)
        {
            using var trx = await _context.Database.BeginTransactionAsync();
            DebitCreditSmsRequest smsData = null;
            try
            {
                var payment = await _context.TblPaymentRequest.FirstOrDefaultAsync(p => p.PaymentId == request.PaymentId);
                if (payment == null) throw new KeyNotFoundException("Payment not found");

                payment.Status = request.Status;
                payment.AdminRemarks = request.AdminRemarks;
                payment.ModifiedBy = request.ModifiedBy;
                payment.ModifiedOn = DateTime.Now;

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
                        decimal newBal = oldBal + payment.Amount ?? 0;

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
                            Remarks = $"Payment approved for Txn {payment.PaymentTxnId}",
                            WlId = user.Wlid,
                            Txndate = DateTime.Now
                        };

                        await _context.Tbluserbalances.AddAsync(walletTxn);
                        payment.openingBalance = oldBal;
                        payment.closingBalance = newBal;
                        payment.TxnId = walletTxn.Id.ToString();

                        smsData = new DebitCreditSmsRequest
                        {
                            TransferType = "Credit",
                            ReceiverPhone = user.Phone,
                            ReceiverPreAmount = oldBal,
                            ReceiverCurrentAmount = newBal,
                            ReceiverName = user.Name,
                            TransactionAmount = payment.Amount
                        };

                    }
                }
                else if (request.Status == "Rejected")
                {
                    if (string.IsNullOrWhiteSpace(request.AdminRemarks))
                        throw new ArgumentException("Admin remarks mandatory when rejecting");

                }

                await _context.SaveChangesAsync();
                await trx.CommitAsync();
                if (smsData != null)
                {
                    await _smsService.SendDebitCreditSmsAsync(smsData);
                }
            }
            catch
            {
                await trx.RollbackAsync();
                throw;
            }
        }

        //public async Task<(IEnumerable<PaymentResponseDto> Payments, int TotalCount)>
        //GetAllPaymentsAsync(int pageNumber, int pageSize, string status, string? fromDate, string? toDate, string commonsearch, int isExport)
        //{
        //    try
        //    {
        //        // Start with the base query
        //        var query = _context.TblPaymentRequest
        //            .Where(p => p.IsDeleted== false);

        //        // Apply status filter
        //        if (!string.IsNullOrWhiteSpace(status))
        //            query = query.Where(p => p.Status == status);

        //        // Apply date filters (only date part)
        //        if (!string.IsNullOrEmpty(fromDate) && DateTime.TryParse(fromDate, out var from))
        //        {
        //            var fromDateOnly = from.Date;
        //            query = query.Where(p => p.CreatedOn >= fromDateOnly);
        //        }

        //        // ✅ Convert toDate (IMPORTANT FIX)
        //        if (!string.IsNullOrEmpty(toDate) && DateTime.TryParse(toDate, out var to))
        //        {
        //            var toDateOnly = to.Date.AddDays(1); // include full day
        //            query = query.Where(p => p.CreatedOn < toDateOnly);
        //        }


        //        // Get total count before pagination
        //        int totalCount = await query.CountAsync();

        //        // Join related tables and select DTO after filtering
        //        var data = await query
        //            .OrderByDescending(p => p.CreatedOn) // optional: latest first
        //            .Skip((pageNumber - 1) * pageSize)
        //            .Take(pageSize)
        //            .Join(_context.BankMaster,
        //                  p => p.BankId,
        //                  b => b.BankId,
        //                  (p, b) => new { p, b })
        //            .Join(_context.TblUsers,
        //                  pb => pb.p.UserId,
        //                  u => u.Id,
        //                  (pb, u) => new PaymentResponseDto
        //                  {
        //                      PaymentId = (Guid)pb.p.PaymentId,
        //                      TxnId = pb.p.TxnId ?? "",
        //                      PaymentTxnId = pb.p.PaymentTxnId ?? "",
        //                      UserName = u.Username+"-"+u.Phone,
        //                      UserType = u.Usertype,
        //                      BankName = pb.b.BankName,
        //                      AccountNo = pb.b.AccountNumber,
        //                      Amount = pb.p.Amount ?? 0,
        //                      DepositeMode = pb.p.DeposideMode,
        //                      TxnSlipFileName = pb.p.TxnSlipFileName,
        //                      TxnSlipPath = pb.p.TxnSlipPath,
        //                      Status = pb.p.Status,
        //                      AdminRemarks = pb.p.AdminRemarks,
        //                      UserRemarks = pb.p.UserRemarks,
        //                      OpeningBalance = pb.p.openingBalance.ToString()??"0",
        //                      ClosingBalance = pb.p.closingBalance.ToString()??"0",
        //                      TxnDate = pb.p.CreatedOn,
        //                      TxnApprovedDate = pb.p.ModifiedOn
        //                  })
        //            .ToListAsync();

        //        return (data, totalCount);
        //    }
        //    catch (Exception ex)
        //    {
        //        throw;
        //    }
        //}

        public async Task<(IEnumerable<PaymentResponseDto> Payments, int TotalCount)>
GetAllPaymentsAsync(int pageNumber, int pageSize, string status, string? fromDate, string? toDate, string commonsearch, int isExport, int userid=0)
        {
            try
            {
                var query = _context.TblPaymentRequest
                    .Where(p => p.IsDeleted == false);

                // ✅ Status filter
                if (!string.IsNullOrWhiteSpace(status))
                    query = query.Where(p => p.Status == status);

                if(userid>0)
                {
                    query = query.Where(p => p.UserId == userid);
                }

                // ✅ Date filter (FIXED)
                if (!string.IsNullOrEmpty(fromDate) && DateTime.TryParse(fromDate, out var from))
                {
                    var fromDateOnly = from.Date;
                    query = query.Where(p => p.CreatedOn >= fromDateOnly);

                    _logger.LogInformation($"Applied FromDate: {fromDateOnly}");
                }              

                if (!string.IsNullOrEmpty(toDate) && DateTime.TryParse(toDate, out var to))
                {
                    var toDateOnly = to.Date.AddDays(1); // include full day
                    query = query.Where(p => p.CreatedOn < toDateOnly);

                    _logger.LogInformation($"Applied ToDate: {toDateOnly}");
                }

                // ✅ JOIN FIRST
                var joinedQuery = query
                    .Join(_context.BankMaster,
                          p => p.BankId,
                          b => b.BankId,
                          (p, b) => new { p, b })
                    .Join(_context.TblUsers,
                          pb => pb.p.UserId,
                          u => u.Id,
                          (pb, u) => new { pb.p, pb.b, u });

                // ✅ Common search
                if (!string.IsNullOrWhiteSpace(commonsearch) && isExport <= 0)
                {
                    var search = commonsearch.Trim();

                    joinedQuery = joinedQuery.Where(x =>
                        x.u.Username.Contains(search) ||
                        x.u.Name.Contains(search) ||
                        x.u.Phone.Contains(search) ||
                        x.p.TxnId.Contains(search) ||
                        x.p.Amount.ToString().Contains(search)
                    );
                }

                // ✅ Total count
                int totalCount = await joinedQuery.CountAsync();

                // ✅ Pagination
                if (isExport <= 0)
                {
                    joinedQuery = joinedQuery
                        .OrderByDescending(x => x.p.CreatedOn)
                        .Skip((pageNumber - 1) * pageSize)
                        .Take(pageSize);
                }
                else
                {
                    joinedQuery = joinedQuery
                        .OrderByDescending(x => x.p.CreatedOn);
                }

                // ✅ Final projection
                var data = await joinedQuery
                    .Select(x => new PaymentResponseDto
                    {
                        PaymentId = (Guid)x.p.PaymentId,
                        TxnId = x.p.TxnId ?? "",
                        PaymentTxnId = x.p.PaymentTxnId ?? "",
                        UserName = x.u.Name + "-" + x.u.Phone,
                        UserType = x.u.Usertype,
                        BankName = x.b.BankName,
                        AccountNo = x.b.AccountNumber,
                        Amount = x.p.Amount ?? 0,
                        DepositeMode = x.p.DeposideMode,
                        TxnSlipFileName = x.p.TxnSlipFileName,
                        TxnSlipPath = x.p.TxnSlipPath,
                        Status = x.p.Status,
                        AdminRemarks = x.p.AdminRemarks,
                        UserRemarks = x.p.UserRemarks,
                        OpeningBalance = x.p.openingBalance.ToString() ?? "0",
                        ClosingBalance = x.p.closingBalance.ToString() ?? "0",
                        TxnDate = x.p.CreatedOn,
                        TxnApprovedDate = x.p.ModifiedOn
                    })
                    .ToListAsync();

                return (data, totalCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetAllPaymentsAsync");
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
            catch (Exception ex)
            {
                throw ex;
            }
        }

        public async Task<PaymentResponseDto> GetPaymentByIdAsync(Guid paymentId)
        {
            try
            {
                var data = await _context.TblPaymentRequest
                    .Where(p => p.IsDeleted == false && p.PaymentId == paymentId)
                    .Join(_context.BankMaster,
                          p => p.BankId,
                          b => b.BankId,
                          (p, b) => new { p, b })
                    .Join(_context.TblUsers,
                          pb => pb.p.UserId,
                          u => u.Id,
                          (pb, u) => new PaymentResponseDto
                          {
                              PaymentId = (Guid)pb.p.PaymentId,
                              TxnId = pb.p.TxnId,
                              UserName = u.Username,
                              UserType = u.Usertype,
                              BankName = pb.b.BankName,
                              AccountNo = pb.b.AccountNumber,
                              Amount = pb.p.Amount ?? 0,
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
}

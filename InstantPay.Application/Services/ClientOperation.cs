using InstantPay.Application.DTOs;
using InstantPay.Application.Interfaces;
using InstantPay.Application.Interfaces.SMS;
using InstantPay.Application.Services.SMS;
using InstantPay.Infrastructure.Security;
using InstantPay.Infrastructure.Sql.Entities;
using InstantPay.SharedKernel.Entity;
using InstantPay.SharedKernel.RequestPayload.DebitCredit;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using MongoDB.Bson.IO;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static InstantPay.SharedKernel.Enums.WalletOperationStatusENUM;

namespace InstantPay.Application.Services
{
    public class ClientOperation : IClientOperation
    {
        private readonly AppDbContext _context;
        private IFileHandler _IFileHandler;
        private ISmsService _smsService;
        private readonly AesEncryptionService _aes;
        private readonly IClientVerificationService _verificationService;
        public ClientOperation(AppDbContext context, IFileHandler iFileHandler, AesEncryptionService aes, ISmsService smsService, IClientVerificationService verificationService)
        {
            _context = context;
            _IFileHandler = iFileHandler;
            _aes = aes;
            _smsService = smsService;
            _verificationService = verificationService;
        }

        public async Task<GetUsersWithMainBalanceResponse> GetClientList(GetUsersWithMainBalanceQuery request)
        {
            DateOnly? fromDate = null;
            DateOnly? toDate = null;

            if (DateOnly.TryParse(request.fromDate, out var parsedFromDate))
                fromDate = parsedFromDate;

            if (DateOnly.TryParse(request.toDate, out var parsedToDate))
                toDate = parsedToDate;

            var balanceQuery = _context.TblWlbalances.AsQueryable();


            if (fromDate.HasValue)
            {
                balanceQuery = balanceQuery.Where(b => b.Txndate.Value.Date >= fromDate.Value.ToDateTime(TimeOnly.MinValue).Date);
            }
            if (toDate.HasValue)
                balanceQuery = balanceQuery.Where(b => b.Txndate.Value.Date <= toDate.Value.ToDateTime(TimeOnly.MinValue).Date);

            // Step 1: Get latest balances by UserId + UserName from DB
            var latestBalances = await balanceQuery
                .GroupBy(b => new { b.UserId})
                .Select(g => g.OrderByDescending(b => b.Id).FirstOrDefault())
                .ToListAsync(); // Materialize here so EF is done

            // Step 2: Build dictionary in memory using tuple key, normalize username
            var balanceDict = latestBalances
            .ToDictionary(
                b => (b.UserId),
                b => b.NewBal ?? 0m // if null, store as 0
            );

            var totalBalance = balanceDict.Values.Sum();

            var userQuery = _context.TblWlUsers.AsQueryable();

            if (!string.IsNullOrWhiteSpace(request.commonsearch))
            {
                var search = request.commonsearch.Trim().ToLower();

                userQuery = userQuery.Where(u =>
                    (u.UserName != null && u.UserName.ToLower().Contains(search)) ||
                    (u.Phone != null && u.Phone.ToLower().Contains(search)) ||
                    (u.CompanyName != null && u.CompanyName.ToLower().Contains(search)) ||
                    (u.EmailId != null && u.EmailId.ToLower().Contains(search)) ||
                    (u.Phone != null && u.Phone.Contains(search)) ||
                    (u.City != null && u.City.ToLower().Contains(search))
                );
            }

            var totalCount = await userQuery.CountAsync();

            // Step 3: Get users
            var users = await userQuery
            .OrderByDescending(u => u.Id)
            .Skip((request.pageIndex - 1) * request.pageSize)
            .Take(request.pageSize)
            .ToListAsync();


            var result = users.Select(u =>
            {
                var lookupKey = (u.Id.ToString().Trim().ToLowerInvariant());
                return new UserBalanceDto
                {
                    Id = u.Id,
                    UserName = u.UserName ?? "",
                    CompanyName = u.CompanyName ?? "",
                    Domain = u.DomainName ?? "",
                    City = u.City ?? "",
                    Status = u.Status ?? "",
                    EmailId = u.EmailId ?? "",
                    MainBalance = balanceDict.TryGetValue(lookupKey, out var bal) ? bal : 0m
                };
            }).ToList();




            return new GetUsersWithMainBalanceResponse
            {
                PageIndex = request.pageIndex,
                PageSize = request.pageSize,
                TotalRecords = totalCount,
                TotalBalance = (decimal)totalBalance,
                Users = result
            };
        }

        public async Task<ResponseModelforClientaddandupdateapi> CreateOrUpdateClient(CreateOrUpdateClientCommand request, CancellationToken cancellationToken)
        {
            TblWlUser client;
            bool isNew = request.ClientId == 0;
            var existingClient = isNew
                ? null
                : await _context.TblWlUsers.FirstOrDefaultAsync(c => c.Id == request.ClientId, cancellationToken);

            if (!isNew && existingClient == null)
            {
                return new ResponseModelforClientaddandupdateapi
                {
                    Msg = "Record Not Found",
                    flag = false
                };
            }

            if (request.CommissionPlanId > 0)
            {
                var planExists = await _context.PlanDetails
                    .AnyAsync(p => p.Id == request.CommissionPlanId && p.IsActive, cancellationToken);
                if (!planExists)
                {
                    return new ResponseModelforClientaddandupdateapi
                    {
                        Msg = "Please select a valid active commission plan.",
                        flag = false
                    };
                }
            }

            var phoneNeedsVerification = isNew
                || !existingClient!.IsPhoneVerified
                || !string.Equals(existingClient.Phone?.Trim(), request.Phone?.Trim(), StringComparison.Ordinal);
            var emailNeedsVerification = isNew
                || !existingClient!.IsEmailVerified
                || !string.Equals(existingClient.EmailId?.Trim(), request.EmailId?.Trim(), StringComparison.OrdinalIgnoreCase);
            var panNeedsVerification = isNew
                || !existingClient!.IsPanVerified
                || !string.Equals(existingClient.PanCard?.Trim(), request.PanCard?.Trim(), StringComparison.OrdinalIgnoreCase);
            var aadhaarNeedsVerification = isNew
                || !existingClient!.IsAadhaarVerified
                || !string.Equals(existingClient.AadharCard?.Trim(), request.AadharCard?.Trim(), StringComparison.Ordinal);

            if (phoneNeedsVerification && !_verificationService.ValidateProof(
                    request.MobileVerificationToken,
                    ClientUserVerificationTypes.Phone,
                    request.Phone))
                return new ResponseModelforClientaddandupdateapi { Msg = "Mobile number verification is required.", flag = false };

            if (emailNeedsVerification && !_verificationService.ValidateProof(
                    request.EmailVerificationToken,
                    ClientUserVerificationTypes.Email,
                    request.EmailId))
                return new ResponseModelforClientaddandupdateapi { Msg = "Email verification is required.", flag = false };

            if (panNeedsVerification && !_verificationService.ValidateProof(
                    request.PanVerificationToken,
                    ClientUserVerificationTypes.Pan,
                    request.PanCard))
                return new ResponseModelforClientaddandupdateapi { Msg = "PAN verification is required.", flag = false };

            if (aadhaarNeedsVerification && !_verificationService.ValidateProof(
                    request.AadharVerificationToken,
                    ClientUserVerificationTypes.Aadhaar,
                    request.AadharCard))
                return new ResponseModelforClientaddandupdateapi { Msg = "Aadhaar verification is required.", flag = false };

            if (isNew)
            {
                var existingUser = await _context.TblWlUsers
        .FirstOrDefaultAsync(x => x.UserName.ToLower().Trim() == request.UserName.ToLower().Trim());

                if (existingUser != null)
                {
                    return new ResponseModelforClientaddandupdateapi
                    {
                        Msg = "Username already exists.",
                        flag = false
                    };
                }
                client = new TblWlUser
                {
                    CompanyName = request.CompanyName,
                    UserName = request.UserName,
                    EmailId = request.EmailId,
                    Phone = request.Phone,
                    //Password = _aes.Encrypt(request.Password),
                    Password = (request.Password),
                    PanCard = request.PanCard,
                    AadharCard = request.AadharCard,
                    DomainName = request.DomainName,
                    AddressLine1 = request.AddressLine1,
                    AddressLine2 = request.AddressLine2,
                    State = request.State,
                    City = request.City,
                    Pincode = request.Pincode,
                    Recharge = request.Recharge,
                    MoneyTransfer = request.MoneyTransfer,
                    Aeps = request.AEPS,
                    BillPayment = request.BillPayment,
                    MicroAtm = request.MicroATM,
                    Apitransfer = request.APITransfer,
                    Margin = request.Margin,
                    Debit = request.Debit,
                    RazorpayPayment = request.RazorpayPayment,
                    Settlement = request.Settlement,
                    Status = "Active",
                    RegDate = DateTime.UtcNow,
                    TxnPin = request.TxnPin,
                    PlanId = request.CommissionPlanId > 0 ? request.CommissionPlanId.ToString() : null,
                    CommissionPlanId = request.CommissionPlanId > 0 ? request.CommissionPlanId : (int?)null,
                    Lat = request.lat,
                    Longitute = request.longitute,
                    IsPhoneVerified = true,
                    PhoneVerifiedAt = DateTime.UtcNow,
                    IsEmailVerified = true,
                    EmailVerifiedAt = DateTime.UtcNow,
                    IsPanVerified = true,
                    PanVerifiedAt = DateTime.UtcNow,
                    PanVerifiedName = _verificationService.GetVerifiedName(request.PanVerificationToken),
                    IsAadhaarVerified = true,
                    AadharVerifiedAt = DateTime.UtcNow,
                };

                _context.TblWlUsers.Add(client);
                await _context.SaveChangesAsync(cancellationToken);
            }
            else
            {
                client = existingClient!;

                var existingUser = await _context.TblWlUsers.FirstOrDefaultAsync(x => x.UserName.ToLower().Trim() == request.UserName.ToLower().Trim() && x.Id != request.ClientId);

                if (existingUser != null)
                {
                    return new ResponseModelforClientaddandupdateapi
                    {
                        Msg = "Username already exists.",
                        flag = false
                    };
                }

                client.CompanyName = request.CompanyName;
                client.UserName = request.UserName;
                client.EmailId = request.EmailId;
                client.Phone = request.Phone;
                //client.Password = _aes.Encrypt(request.Password);
                client.Password = (request.Password);
                client.PanCard = request.PanCard;
                client.AadharCard = request.AadharCard;
                client.DomainName = request.DomainName;
                client.AddressLine1 = request.AddressLine1;
                client.AddressLine2 = request.AddressLine2;
                client.State = request.State;
                client.City = request.City;
                client.Pincode = request.Pincode;
                client.Recharge = request.Recharge;
                client.MoneyTransfer = request.MoneyTransfer;
                client.Aeps = request.AEPS;
                client.BillPayment = request.BillPayment;
                client.MicroAtm = request.MicroATM;
                client.Apitransfer = request.APITransfer;
                client.Margin = request.Margin;
                client.Debit = request.Debit;
                client.RazorpayPayment = request.RazorpayPayment;
                client.Settlement = request.Settlement;
                client.Status = request.Status;
                client.TxnPin = request.TxnPin;
                if (request.CommissionPlanId > 0)
                {
                    client.PlanId = request.CommissionPlanId.ToString();
                    client.CommissionPlanId = request.CommissionPlanId;
                }
                client.Lat = request.lat;
                client.Longitute = request.longitute;

                if (phoneNeedsVerification)
                {
                    client.IsPhoneVerified = true;
                    client.PhoneVerifiedAt = DateTime.UtcNow;
                }
                if (emailNeedsVerification)
                {
                    client.IsEmailVerified = true;
                    client.EmailVerifiedAt = DateTime.UtcNow;
                }
                if (panNeedsVerification)
                {
                    client.IsPanVerified = true;
                    client.PanVerifiedAt = DateTime.UtcNow;
                    client.PanVerifiedName = _verificationService.GetVerifiedName(request.PanVerificationToken);
                }
                if (aadhaarNeedsVerification)
                {
                    client.IsAadhaarVerified = true;
                    client.AadharVerifiedAt = DateTime.UtcNow;
                }
            }

            string webRootPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
            string basePath = Path.Combine(webRootPath, "UploadFiles", client.Id.ToString());
            Directory.CreateDirectory(basePath);
            string? SaveFile(IFormFile file, string folder)
            {
                if (file == null) return null;
                string folderPath = Path.Combine(basePath, folder);
                Directory.CreateDirectory(folderPath);
                var safeFileName = $"{Guid.NewGuid():N}{Path.GetExtension(file.FileName).ToLowerInvariant()}";
                string filePath = Path.Combine(folderPath, safeFileName);
                using var stream = new FileStream(filePath, FileMode.Create);
                file.CopyTo(stream);
                return Path.Combine("UploadFiles", client.Id.ToString(), folder, safeFileName).Replace("\\", "/");
            }
            string? panPath = "";
            string? aadharPath = "";
            string? aadharBackPath = "";
            string? logopath = "";
            if (request.PancopyFile != null)
            {
                panPath = SaveFile(request.PancopyFile, "PanCard");
                client.Pancopy = panPath ?? "";
            }
            if (request.AadharFrontFile != null)
            {
                aadharPath = SaveFile(request.AadharFrontFile, "AadharCard");
                client.AadharFront = aadharPath ?? "";
            }
            if (request.AadharBackFile != null)
            {
                aadharBackPath = SaveFile(request.AadharBackFile, "AadharBack");
                client.AadharBack = aadharBackPath ?? "";
            }
            if (request.LogoFile != null)
            {
                logopath = SaveFile(request.LogoFile, "Logo");
                client.Logo = logopath ?? "";
            }
            if (request.SelfieFile != null)
            {
                client.SelfieImage = SaveFile(request.SelfieFile, "Selfie") ?? "";
            }

            await _context.SaveChangesAsync(cancellationToken);
            _verificationService.ConsumeProof(request.MobileVerificationToken);
            _verificationService.ConsumeProof(request.EmailVerificationToken);
            _verificationService.ConsumeProof(request.PanVerificationToken);
            _verificationService.ConsumeProof(request.AadharVerificationToken);

            return new ResponseModelforClientaddandupdateapi
            {
                id = client.Id,
                Msg = isNew ? "Client Created Successfully" : "Client Updated",
                flag = true
            };
        }

        public async Task<GetClientDetail?> GetClientDetailByIdAsync(int Id)
        {
            var client = await _context.TblWlUsers
                .Where(c => c.Id == Id)
                .Select(c => new GetClientDetail
                {
                    Id = c.Id,
                    CompanyName = c.CompanyName,
                    UserName = c.UserName,
                    EmailId = c.EmailId,
                    Phone = c.Phone,
                    //Password = _aes.Decrypt(c.Password),
                    Password = c.Password,
                    PanCard = c.PanCard,
                    AadharCard = c.AadharCard,
                    DomainName = c.DomainName,
                    Logo = c.Logo,
                    SelfieImage = c.SelfieImage,
                    AddressLine1 = c.AddressLine1,
                    AddressLine2 = c.AddressLine2,
                    State = c.State,
                    City = c.City,
                    Pincode = c.Pincode,
                    Pancopy = c.Pancopy,
                    AadharFront = c.AadharFront,
                    AadharBack = c.AadharBack,
                    Recharge = c.Recharge,
                    MoneyTransfer = c.MoneyTransfer,
                    AEPS = c.Aeps,
                    BillPayment = c.BillPayment,
                    MicroATM = c.MicroAtm,
                    APITransfer = c.Apitransfer,
                    Margin = c.Margin,
                    Debit = c.Debit,
                    RazorpayPayment = c.RazorpayPayment,
                    Settlement = c.Settlement,
                    Status = c.Status,
                    RegDate = c.RegDate,
                    TxnPin = c.TxnPin,
                    PlanId = c.PlanId,
                    CommissionPlanId = c.CommissionPlanId,
                    Lat = c.Lat,
                    Longitute = c.Longitute,
                    IsPhoneVerified = c.IsPhoneVerified,
                    IsEmailVerified = c.IsEmailVerified,
                    IsPanVerified = c.IsPanVerified,
                    PanVerifiedName = c.PanVerifiedName,
                    IsAadhaarVerified = c.IsAadhaarVerified
                })
        .FirstOrDefaultAsync();

            return client;
        }

        public async Task<ResponseModelforClientaddandupdateapi> Handle(DeleteClientFileCommand request, CancellationToken cancellationToken)
        {
            var client = await _context.TblWlUsers.FindAsync(new object[] { request.ClientId }, cancellationToken);

            if (client == null)
            {
                return new ResponseModelforClientaddandupdateapi
                {
                    id = request.ClientId,
                    Msg = "Client Not Found",
                    flag = false
                };
            }

            string? filePath = null;
            switch (request.FileType)
            {
                case "LogoFile":
                    filePath = client.Logo;
                    client.Logo = "";
                    break;
                case "PancopyFile":
                    filePath = client.Pancopy;
                    client.Pancopy = "";
                    break;
                case "AadharFrontFile":
                    filePath = client.AadharFront;
                    client.AadharFront = "";
                    break;
                case "AadharBackFile":
                    filePath = client.AadharBack;
                    client.AadharBack = "";
                    break;
                case "SelfieFile":
                    filePath = client.SelfieImage;
                    client.SelfieImage = "";
                    break;
                default:
                    return new ResponseModelforClientaddandupdateapi
                    {
                        id = request.ClientId,
                        Msg = "Invalid File Type",
                        flag = false
                    };
            }

            if (!string.IsNullOrWhiteSpace(filePath))
            {
                _IFileHandler.DeleteFile(request.ClientId, filePath);
            }

            await _context.SaveChangesAsync(cancellationToken);
            return new ResponseModelforClientaddandupdateapi
            {
                id = request.ClientId,
                Msg = "File Deleted Successfully",
                flag = true
            };
        }

        public async Task<WalletTransactionResponse> Handle(WalletTransactionRequest request)
        {
            var dto = request;
            using var transaction = await _context.Database.BeginTransactionAsync();
            {
                try
                {
                    var user = await _context.TblWlUsers
                        .Where(x => x.Id == dto.UserId)
                        .Select(x => new { x.Id, x.UserName, x.Phone })
                        .FirstOrDefaultAsync();

                    if (user is null)
                    {
                        return new WalletTransactionResponse { ErrorMessage = "User not found.", IsSuccessful = false };
                    }

                    var admin = await _context.TblSuperadmins
                        .Where(x => x.Id == dto.ActionById)
                        .Select(x => x.TxnPin)
                        .FirstOrDefaultAsync();

                    if (admin is null)
                    {
                        return new WalletTransactionResponse { ErrorMessage = "Admin not found.", IsSuccessful = false };
                    }

                    if (!string.Equals(dto.TxnPin.Trim(), admin.Trim(), StringComparison.Ordinal))
                    {
                        return new WalletTransactionResponse { ErrorMessage = "Invalid Txn Pin", IsSuccessful = false };
                    }

                    var oldBalance = await _context.TblWlbalances
                        .Where(b => Convert.ToInt32(b.UserId) == dto.UserId)
                        .OrderByDescending(b => b.Id)
                        .Select(b => b.NewBal)
                        .FirstOrDefaultAsync();

                    var newBalance = dto.Status == WalletOperationStatus.Credit ? (oldBalance ?? 0) + dto.Amount : (oldBalance ?? 0) - dto.Amount;
                    var txnType = dto.Status == WalletOperationStatus.Credit ? "WALLET TOPUP BY ADMIN" : "WALLET DEBIT BY ADMIN";
                    var remarks = $"{txnType} For Account No {user.Phone} | {(dto.Status == WalletOperationStatus.Credit ? "Credit" : "Debit")} by Services | Wallet {(dto.Status == WalletOperationStatus.Credit ? "TopUp" : "Debit")} BY Admin Account";

                    var walletIns = new TblWlbalance
                    {
                        TxnAmount = dto.Amount,
                        SurComm = 0,
                        Tds = 0,
                        UserId = Convert.ToString(dto.UserId),
                        UserName = user.UserName+"-"+user.Phone,
                        OldBal = oldBalance,
                        Amount = dto.Amount,
                        NewBal = newBalance,
                        TxnType = txnType,
                        CrdrType = dto.Status == WalletOperationStatus.Credit ? "Credit" : "Debit",
                        Remarks = remarks,
                        Txndate = DateTime.Now
                    };
                    _context.TblWlbalances.Add(walletIns);
                    await _context.SaveChangesAsync();

                    var payment = new TblPaymentRequest
                    {
                        PaymentId = Guid.NewGuid(),
                        BankId = Guid.Parse("61A14EEF-9765-45BA-AD22-ADE44D01F708"),
                        UserId = request.UserId,
                        Amount = request.Amount,
                        TxnId = walletIns.Id.ToString(),
                        DeposideMode = dto.Status == WalletOperationStatus.Credit ? "BORROW Credit by Admin" : "Debit By Admin",
                        Status = "Approved",
                        CreatedBy = request.ActionById,
                        CreatedOn = DateTime.Now,
                        ModifiedOn = DateTime.Now,
                        IsDeleted = false,
                        UserRemarks = "",
                        AdminRemarks = dto.Status == WalletOperationStatus.Credit ? "Paid By Admin as Request by you as a borrow" : "Debit By Admin",
                        openingBalance = oldBalance,
                        closingBalance = newBalance
                    };

                    _context.TblPaymentRequest.Add(payment);

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    var smsData = new DebitCreditSmsRequest
                    {
                        TransferType = dto.Status == WalletOperationStatus.Credit ? "Credit" : "Debit",
                        ReceiverPhone = user.Phone,
                        ReceiverPreAmount = oldBalance ?? 0,
                        ReceiverCurrentAmount = newBalance,
                        ReceiverName = user.UserName,
                        TransactionAmount = dto.Amount
                    };

                    await _smsService.SendDebitCreditSmsAsync(smsData);

                    return new WalletTransactionResponse
                    {
                        Username = user.UserName,
                        Oldbalance = Convert.ToString(oldBalance),
                        NewBalance = Convert.ToString(newBalance),
                        Amount = Convert.ToString(dto.Amount),
                        TxnType = Convert.ToString(txnType),
                        CrdrType = Convert.ToString(dto.Status == WalletOperationStatus.Credit ? "Credit" : "Debit"),
                        Remarks = Convert.ToString(remarks),
                        Txndate = DateTime.Now,
                        ErrorMessage = dto.Status == WalletOperationStatus.Credit ? "Balance Credited Successfully" : "Balance Debited Successfully",
                        IsSuccessful = true
                    };
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    return new WalletTransactionResponse { ErrorMessage = ex.Message, IsSuccessful = false };
                }
            }
        }

    }
}

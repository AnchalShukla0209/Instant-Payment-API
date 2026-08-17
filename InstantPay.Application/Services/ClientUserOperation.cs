using InstantPay.Application.Interfaces;
using InstantPay.Application.Interfaces.SMS;
using InstantPay.Application.DTOs;
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
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using static InstantPay.SharedKernel.Enums.WalletOperationStatusENUM;
using static MongoDB.Driver.WriteConcern;
using Microsoft.Extensions.Logging;

namespace InstantPay.Application.Services
{
    public class ClientUserOperation : IClientUserOperation
    {
        private readonly AppDbContext _context;
        private IFileHandler _IFileHandler;
        private readonly AesEncryptionService _aes;
        private readonly ISmsService _smsService;
        private readonly IWalletService _walletService;
        private readonly IClientUserVerificationService _verificationService;
        private readonly IEmailService _emailService;
        private readonly ILogger<ClientUserOperation> _logger;
        public ClientUserOperation(
            AppDbContext context,
            IFileHandler iFileHandler,
            AesEncryptionService aes,
            ISmsService smsservice,
            IWalletService walletService,
            IClientUserVerificationService verificationService,
            IEmailService emailService,
            ILogger<ClientUserOperation> logger)
        {
            _context = context;
            _IFileHandler = iFileHandler;
            _aes = aes;
            _smsService = smsservice;
            _walletService = walletService;
            _verificationService = verificationService;
            _emailService = emailService;
            _logger = logger;
        }

        public async Task<GetClientUsersWithMainBalanceResponse> GetClientUserList(GetClientUserQuery request)
        {
            DateTime? fromDate = null;
            DateTime? toDate = null;

            if (!string.IsNullOrEmpty(request.fromDate))
                fromDate = DateTime.Parse(request.fromDate);

            if (!string.IsNullOrEmpty(request.toDate))
                toDate = DateTime.Parse(request.toDate);

            var balanceFiltered = _context.Tbluserbalances
                .Where(b =>
                    (!fromDate.HasValue || b.Txndate >= fromDate) &&
                    (!toDate.HasValue || b.Txndate <= toDate)
                );

            // first: get latest balance record per user
            var latestBalanceIds =
                from b in balanceFiltered
                group b by b.UserId into g
                select new
                {
                    UserId = g.Key,
                    LatestId = g.Max(x => x.Id)
                };

            // join to get NewBal only once
            var latestBalances =
                from x in latestBalanceIds
                join b in _context.Tbluserbalances on x.LatestId equals b.Id
                select new
                {
                    x.UserId,
                    b.NewBal
                };

            

            var scopeId = request.ClientId.ToString();
            var baseUsers = string.Equals(request.ScopeType, "AD", StringComparison.OrdinalIgnoreCase)
                ? _context.TblUsers.Where(t => t.Adid == scopeId)
                : string.Equals(request.ScopeType, "MD", StringComparison.OrdinalIgnoreCase)
                    ? _context.TblUsers.Where(t => t.Mdid == scopeId)
                    : _context.TblUsers.Where(t => t.Wlid == scopeId);

            baseUsers = baseUsers.Where(t =>
                (!fromDate.HasValue || t.RegDate >= fromDate) &&
                (!toDate.HasValue || t.RegDate <= toDate)
            );

            if (!string.IsNullOrWhiteSpace(request.commonsearch))
            {
                var search = request.commonsearch.Trim();

                baseUsers = baseUsers.Where(t =>
                    EF.Functions.Like(t.Username ?? "", $"%{search}%") ||
                    EF.Functions.Like(t.Phone ?? "", $"%{search}%") ||
                    EF.Functions.Like(t.CompanyName ?? "", $"%{search}%") ||
                    EF.Functions.Like(t.EmailId ?? "", $"%{search}%") ||
                    EF.Functions.Like(t.Phone ?? "", $"%{search}%") ||
                    EF.Functions.Like(t.City ?? "", $"%{search}%")
                );
            }

            // Computed AFTER the search filter so "Total Balance" always matches the exact
            // set of users currently listed/searched, instead of the full unfiltered scope.
            var filteredLatestBalances =
            from lb in latestBalances
            join u in baseUsers on lb.UserId equals u.Id
            select lb;

            var totalBalance = await filteredLatestBalances.SumAsync(x => x.NewBal ?? 0m);

            var totalCount = await baseUsers.CountAsync();

            var usersPaged =
                await (
                    from t1 in baseUsers
                    join cp in _context.PlanDetails on t1.CommissionPlanId equals cp.Id into cpj
                    from cp in cpj.DefaultIfEmpty()
                    join t2 in _context.TblUsers on t1.Adid equals t2.Id.ToString() into adJ
                    from t2 in adJ.DefaultIfEmpty()
                    join t3 in _context.TblUsers on t1.Mdid equals t3.Id.ToString() into mdJ
                    from t3 in mdJ.DefaultIfEmpty()
                    join lb in latestBalances on t1.Id equals lb.UserId into lbj
                    from lb in lbj.DefaultIfEmpty()
                    orderby t1.Id descending
                    select new UserBalanceRec
                    {
                        Id = t1.Id,
                        UserName = t1.Username ?? "",
                        Name = t1.Name??"",
                        UserType = t1.Usertype,
                        Phone = t1.Phone,
                        CompanyName = t1.CompanyName ?? "",
                        City = t1.City ?? "",
                        Status = t1.Status ?? "",
                        EmailId = t1.EmailId ?? "",
                        PlanName = cp != null ? cp.PlanName : "",
                        ADName = t2 != null ? t2.Name : "NA",
                        MDName = t3 != null ? t3.Name : "NA",
                        CreatedDate = (DateTime)t1.RegDate,
                        MainBalance = lb != null ? lb.NewBal ?? 0m : 0m
                    }
                )
                .Skip((request.pageIndex - 1) * request.pageSize)
                .Take(request.pageSize)
                .AsNoTracking()
                .ToListAsync();

            return new GetClientUsersWithMainBalanceResponse
            {
                PageIndex = request.pageIndex,
                PageSize = request.pageSize,
                TotalRecords = totalCount,
                TotalBalance = totalBalance,
                Users = usersPaged
            };
        }



        public async Task<ResponseModelforClientUseraddandupdateapi> CreateOrUpdateClientUser(CreateOrUpdateClientUserCommand request, CancellationToken cancellationToken)
        {
            request.UserType = request.UserType?.Trim().ToUpperInvariant();
            if (request.UserType is not ("RT" or "AD" or "MD" or "ST"))
                return Failure("User type must be RT, AD, MD, or ST.");

            TblUser client;
            bool isNew = request.ClientId == 0;
            var existingClient = isNew
                ? null
                : await _context.TblUsers.FirstOrDefaultAsync(c => c.Id == request.ClientId, cancellationToken);

            if (!isNew && existingClient == null)
                return Failure("Record Not Found");

            var uploadValidationError =
                ValidateUpload(request.LogoFile, false)
                ?? ValidateUpload(request.SelfieFile, false)
                ?? ValidateUpload(request.PancopyFile, true)
                ?? ValidateUpload(request.AadharFrontFile, true)
                ?? ValidateUpload(request.AadharBackFile, true);
            if (uploadValidationError != null)
                return Failure(uploadValidationError);

            var planExists = await _context.PlanDetails
                .AnyAsync(p => p.Id == request.CommissionPlanId && p.IsActive, cancellationToken);
            if (!planExists)
                return Failure("Please select a valid active commission plan.");

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
                return Failure("Mobile number verification is required.");

            if (emailNeedsVerification && !_verificationService.ValidateProof(
                    request.EmailVerificationToken,
                    ClientUserVerificationTypes.Email,
                    request.EmailId))
                return Failure("Email verification is required.");

            if (panNeedsVerification && !_verificationService.ValidateProof(
                    request.PanVerificationToken,
                    ClientUserVerificationTypes.Pan,
                    request.PanCard))
                return Failure("PAN verification is required.");

            if (aadhaarNeedsVerification && !_verificationService.ValidateProof(
                    request.AadharVerificationToken,
                    ClientUserVerificationTypes.Aadhaar,
                    request.AadharCard))
                return Failure("Aadhaar verification is required.");

            if (isNew)
            {
                var existingUser = await _context.TblUsers
        .FirstOrDefaultAsync(x => x.Username.ToLower().Trim() == request.UserName.ToLower().Trim() || x.Phone.Trim() == request.Phone.Trim());

                if (existingUser != null)
                {
                    return new ResponseModelforClientUseraddandupdateapi
                    {
                        Msg = "Username already exists with same username or mobile no.",
                        flag = false
                    };
                }
                client = new TblUser
                {
                    Usertype = request.UserType,
                    CompanyName = request.CompanyName,
                    Name = request.CustomerName,
                    FatherName = request.FatherName,
                    Username = request.UserName,
                    EmailId = request.EmailId,
                    Phone = request.Phone,
                    //Password = _aes.Encrypt(request.Password),
                    Password = (request.Password),
                    PanCard = request.PanCard,
                    AadharCard = request.AadharCard,

                    AddressLine1 = request.AddressLine1,
                    AddressLine2 = request.AddressLine2,
                    State = request.State,
                    City = request.City,
                    Pincode = request.Pincode,

                    ShopAddress = request.ShopAddress,
                    ShopState = request.ShopState,
                    ShopCity = request.ShopCity,
                    ShipZipcode = request.ShopZipCode,

                    MobileRecharge = request.Recharge,
                    MoneyTransfer = request.MoneyTransfer,
                    Aeps = request.AEPS,
                    BillPayment = request.BillPayment,
                    MicroAtm = request.MicroATM,
                    RazorpayPayment = request.RazorpayPayment,
                    Settlement = request.Settlement,

                    Status = "Active",

                    RegDate = DateTime.UtcNow,
                    TxnPin = request.TxnPin,
                    PlanId = request.CommissionPlanId.ToString(),
                    CommissionPlanId = request.CommissionPlanId,
                    Wlid = Convert.ToString(request.WLID),
                    MerchargeCode = "",
                    TokenKey = "",
                    Mdid = string.Equals(request.ScopeType, "MD", StringComparison.OrdinalIgnoreCase)
                        ? ResolveScopePartnerId(request)
                        : request.MDId ?? "0",
                    Adid = string.Equals(request.ScopeType, "AD", StringComparison.OrdinalIgnoreCase)
                        ? ResolveScopePartnerId(request)
                        : request.ADId ?? "0",
                    Stid = request.STId ?? "0",
                    DeviceInfo = "",
                    DeviceId = "",
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
                    //MPin = _aes.Encrypt(request.MPin)
                    MPin = (request.MPin)

                };

                _context.TblUsers.Add(client);
                await _context.SaveChangesAsync(cancellationToken);
            }
            else
            {
                client = existingClient!;

                var existingUser = await _context.TblUsers
        .FirstOrDefaultAsync(x => x.Username.ToLower().Trim() == request.UserName.ToLower().Trim() && x.Id != request.ClientId);

                if (existingUser != null)
                {
                    return new ResponseModelforClientUseraddandupdateapi
                    {
                        Msg = "Username already exists.",
                        flag = false
                    };
                }

                client.CompanyName = request.CompanyName;
                client.Name = request.CustomerName;
                client.FatherName = request.FatherName;
                client.Username = request.UserName;
                client.EmailId = request.EmailId;
                client.Phone = request.Phone;
                //client.Password = _aes.Encrypt(request.Password);
                client.Password = (request.Password);
                client.PanCard = request.PanCard;
                client.AadharCard = request.AadharCard;
                client.Usertype = request.UserType;
                client.AddressLine1 = request.AddressLine1;
                client.AddressLine2 = request.AddressLine2;
                client.State = request.State;
                client.City = request.City;
                client.Pincode = request.Pincode;
                client.ShopAddress = request.ShopAddress;
                client.ShopState = request.ShopState;
                client.ShopCity = request.ShopCity;
                client.ShipZipcode = request.ShopZipCode;

                client.MobileRecharge = request.Recharge;
                client.MoneyTransfer = request.MoneyTransfer;
                client.Aeps = request.AEPS;
                client.BillPayment = request.BillPayment;
                client.MicroAtm = request.MicroATM;
                client.RazorpayPayment = request.RazorpayPayment;
                client.Settlement = request.Settlement;
                client.Status = request.Status;

                client.PlanId = request.CommissionPlanId.ToString();
                client.CommissionPlanId = request.CommissionPlanId;
                client.Wlid = Convert.ToString(request.WLID);
                client.MerchargeCode = "";
                client.TokenKey = "";
                client.Adid = string.Equals(request.ScopeType, "AD", StringComparison.OrdinalIgnoreCase)
                    ? ResolveScopePartnerId(request)
                    : request.ADId ?? "0";
                client.Mdid = string.Equals(request.ScopeType, "MD", StringComparison.OrdinalIgnoreCase)
                    ? ResolveScopePartnerId(request)
                    : request.MDId ?? "0";
                client.Stid = request.STId ?? "0";
                client.DeviceInfo = "";
                client.DeviceId = "";
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
                //client.MPin = _aes.Encrypt(request.MPin);
                client.MPin = (request.MPin);
            }

            string webRootPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
            string basePath = Path.Combine(webRootPath, "UploadFiles", "ClientUser", client.Id.ToString());
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
                return Path.Combine("UploadFiles", "ClientUser", client.Id.ToString(), folder, safeFileName).Replace("\\", "/");
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

            if (isNew && !string.IsNullOrWhiteSpace(client.EmailId))
            {
                var loginUrl = GetLoginUrl(client.Usertype);
                try
                {
                    var emailResult = await _emailService.SendNewUserWelcomeEmailAsync(
                        client.EmailId,
                        client.Name ?? client.Username ?? "User",
                        client.Username ?? client.Id.ToString(),
                        client.Phone ?? string.Empty,
                        client.Usertype ?? string.Empty,
                        loginUrl);
                    if (emailResult != "1")
                        _logger.LogWarning("Welcome email failed for newly created user {UserId}: {EmailResult}", client.Id, emailResult);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Welcome email failed for newly created user {UserId}", client.Id);
                }
            }

            _verificationService.ConsumeProof(request.MobileVerificationToken);
            _verificationService.ConsumeProof(request.EmailVerificationToken);
            _verificationService.ConsumeProof(request.PanVerificationToken);
            _verificationService.ConsumeProof(request.AadharVerificationToken);

            return new ResponseModelforClientUseraddandupdateapi
            {
                id = client.Id,
                Msg = isNew ? "Client Created Successfully" : "Client Updated",
                flag = true
            };
        }

        private static string GetLoginUrl(string? userType) =>
            userType?.Trim().ToUpperInvariant() switch
            {
                "AD" => "https://instantpayment.in/distributor-login",
                "MD" => "https://instantpayment.in/masterdistributor-login",
                "ST" => "https://instantpayment.in/salesteam-login",
                _ => "https://instantpayment.in/login"
            };

        public async Task<GetClientUserDetail?> GetClientUserDetailByIdAsync(int Id)
        {
            var client = await (
            from t1 in _context.TblUsers
            join t2 in _context.TblUsers on t1.Adid equals Convert.ToString(t2.Id) into adJ
            from t2 in adJ.DefaultIfEmpty()
            join t3 in _context.TblUsers on t1.Mdid equals Convert.ToString(t3.Id) into mdJ
            from t3 in mdJ.DefaultIfEmpty()
            join sa in _context.TblWlUsers on t1.Wlid equals Convert.ToString(sa.Id) into saJ
            from sa in saJ.DefaultIfEmpty()
            where t1.Id == Id
            select new GetClientUserDetail
            {
                Id = t1.Id,
                CompanyName = t1.CompanyName,
                UserName = t1.Username,
                EmailId = t1.EmailId,
                Phone = t1.Phone,
                Password = t1.Password,
                PanCard = t1.PanCard,
                AadharCard = t1.AadharCard,
                CustomerName = t1.Name,
                FatherName = t1.FatherName,
                WLId = t1.Wlid,
                ADId = t1.Adid,
                MDId = t1.Mdid,
                STId = t1.Stid,
                UserType = t1.Usertype,
                Logo = t1.Logo,
                SelfieImage = t1.SelfieImage,
                AddressLine1 = t1.AddressLine1,
                AddressLine2 = t1.AddressLine2,
                State = t1.State,
                City = t1.City,
                MDName = t3 != null ? t3.Username : string.Empty,
                ADName = t2 != null ? t2.Username : string.Empty,
                ADMINName = sa != null ? sa.UserName : string.Empty,
                ShopAddress = t1.ShopAddress,
                ShopCity = t1.ShopCity,
                ShopState = t1.ShopState,
                ShopZipCode = t1.ShipZipcode,
                Pincode = t1.Pincode,
                Pancopy = t1.Pancopy,
                AadharFront = t1.AadharFront,
                AadharBack = t1.AadharBack,
                MobileRecharge = t1.MobileRecharge,
                MoneyTransfer = t1.MoneyTransfer,
                AEPS = t1.Aeps,
                BillPayment = t1.BillPayment,
                MicroATM = t1.MicroAtm,
                RazorpayPayment = t1.RazorpayPayment,
                Settlement = t1.Settlement,
                Status = t1.Status,
                Lat = t1.Lat,
                Longitute = t1.Longitute,
                CommissionPlanId = t1.CommissionPlanId,
                IsPhoneVerified = t1.IsPhoneVerified,
                IsEmailVerified = t1.IsEmailVerified,
                IsPanVerified = t1.IsPanVerified,
                PanVerifiedName = t1.PanVerifiedName,
                IsAadhaarVerified = t1.IsAadhaarVerified,
                RegDate = t1.RegDate,
                TxnPin = t1.TxnPin,
                ClientId = t1.Id,
                MPin = t1.MPin
            }).FirstOrDefaultAsync();
            return client;
        }

        public async Task<ResponseModelforClientUseraddandupdateapi> HandleDeleteClientUserFile(DeleteClientUserFileCommand request, CancellationToken cancellationToken)
        {
            var client = await _context.TblUsers.FindAsync(new object[] { request.ClientId }, cancellationToken);

            if (client == null)
            {
                return new ResponseModelforClientUseraddandupdateapi
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
                    return new ResponseModelforClientUseraddandupdateapi
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
            return new ResponseModelforClientUseraddandupdateapi
            {
                id = request.ClientId,
                Msg = "File Deleted Successfully",
                flag = true
            };
        }

        public async Task<WalletTransactionResponse> AddWalletToClientUser(WalletTransactionRequest request)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();
            DebitCreditSmsRequest smsData = null;

            try
            {
                // ✅ Step 1: Validate user
                var user = await _context.TblUsers
                    .Where(x => x.Id == request.UserId)
                    .Select(x => new { x.Id, x.Username, x.Phone, x.Name })
                    .FirstOrDefaultAsync();

                if (user is null)
                {
                    return new WalletTransactionResponse { ErrorMessage = "User not found.", IsSuccessful = false };
                }

                // ✅ Step 2: Validate admin & TxnPin (SuperAdmin, or a Distributor/Master Distributor acting on their own network)
                var adminPin = await _context.TblSuperadmins
                    .Where(x => x.Id == request.ActionById)
                    .Select(x => x.TxnPin)
                    .FirstOrDefaultAsync();

                if (adminPin is null)
                {
                    adminPin = await _context.TblUsers
                        .Where(x => x.Id == request.ActionById)
                        .Select(x => x.TxnPin)
                        .FirstOrDefaultAsync();
                }

                if (adminPin is null)
                {
                    return new WalletTransactionResponse { ErrorMessage = "Admin not found.", IsSuccessful = false };
                }

                if (!string.Equals(request.TxnPin?.Trim(), adminPin.Trim(), StringComparison.Ordinal))
                {
                    return new WalletTransactionResponse { ErrorMessage = "Invalid Txn Pin", IsSuccessful = false };
                }

                // ✅ Steps 3-5: Atomically read balance and insert wallet entry (race-condition-safe via WalletService)
                bool isCredit = request.Status == WalletOperationStatus.Credit;
                var txnType = isCredit ? "WALLET TOPUP BY ADMIN" : "WALLET DEBIT BY ADMIN";
                var crdrType = isCredit ? "Credit" : "Debit";
                var remarks = $"{request.remarks} | {txnType} For Account No {user.Phone} | {crdrType} by Services | Wallet {crdrType} BY Admin Account";
                var userName = user.Name + "-" + user.Phone;

                var (oldBalance, newBalance, walletEntryId) = isCredit
                    ? await _walletService.CreditAsync(request.UserId, userName,
                        request.Amount, request.Amount, 0, 0, txnType, remarks)
                    : await _walletService.DebitAsync(request.UserId, userName,
                        request.Amount, request.Amount, 0, 0, txnType, remarks);

                var payment = new TblPaymentRequest
                {
                    PaymentId = Guid.NewGuid(),
                    BankId = Guid.Parse("61A14EEF-9765-45BA-AD22-ADE44D01F708"),
                    UserId = request.UserId,
                    Amount = request.Amount,
                    TxnId = walletEntryId.ToString(),
                    DeposideMode = isCredit? "BORROW Credit by Admin": "Debit By Admin",
                    Status = "Approved",
                    CreatedBy = request.ActionById,
                    CreatedOn = DateTime.Now,
                    ModifiedOn = DateTime.Now,
                    IsDeleted = false,
                    UserRemarks = "",
                    AdminRemarks = isCredit? "Paid By Admin as Request by you as a borrow" : "Debit By Admin",
                    openingBalance= oldBalance,
                    closingBalance = newBalance

                };

                _context.TblPaymentRequest.Add(payment);

                smsData = new DebitCreditSmsRequest
                {
                    TransferType = crdrType,
                    ReceiverPhone = user.Phone,
                    ReceiverPreAmount = oldBalance,
                    ReceiverCurrentAmount = newBalance,
                    ReceiverName = user.Name,
                    TransactionAmount = request.Amount
                };

                // ✅ Save + Commit
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                if (smsData != null)
                {
                   await _smsService.SendDebitCreditSmsAsync(smsData);
                }

                return new WalletTransactionResponse
                {
                    Username = user.Username,
                    Oldbalance = oldBalance.ToString("F2"),
                    NewBalance = newBalance.ToString("F2"),
                    Amount = request.Amount.ToString("F2"),
                    TxnType = txnType,
                    CrdrType = crdrType,
                    Remarks = remarks,
                    Txndate = DateTime.Now,
                    ErrorMessage = isCredit ? "Balance Credited Successfully" : "Balance Debited Successfully",
                    IsSuccessful = true
                };
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return new WalletTransactionResponse { ErrorMessage = ex.Message, IsSuccessful = false };
            }
        }

        /// <summary>
        /// Peer-to-peer wallet transfer used exclusively by the Distributor/Master Distributor
        /// "Pay" feature. Unlike <see cref="AddWalletToClientUser"/> (a one-sided WL-Admin top-up
        /// with no counterparty), this ALWAYS moves money between two real wallets:
        ///   Credit = ActionById (AD/MD) is debited, UserId (downline) is credited — i.e. "Pay downline".
        ///   Debit  = UserId (downline) is debited, ActionById (AD/MD) is credited — i.e. "Collect from downline".
        /// Both legs happen inside a single DB transaction; if the debited party has insufficient
        /// balance, or anything else fails, the whole transaction is rolled back so it is never
        /// possible for one wallet to be debited without the other being credited (or vice versa).
        /// </summary>
        public async Task<WalletTransactionResponse> TransferWalletForPartnerAsync(WalletTransactionRequest request)
        {
            if (request.Amount <= 0)
            {
                return new WalletTransactionResponse { ErrorMessage = "Amount should be greater than 0.", IsSuccessful = false };
            }

            if (request.UserId == request.ActionById)
            {
                return new WalletTransactionResponse { ErrorMessage = "Cannot transfer to your own account.", IsSuccessful = false };
            }

            var target = await _context.TblUsers
                .Where(x => x.Id == request.UserId)
                .Select(x => new { x.Id, x.Username, x.Phone, x.Name })
                .FirstOrDefaultAsync();
            if (target is null)
            {
                return new WalletTransactionResponse { ErrorMessage = "User not found.", IsSuccessful = false };
            }

            var actor = await _context.TblUsers
                .Where(x => x.Id == request.ActionById)
                .Select(x => new { x.Id, x.Username, x.Phone, x.Name, x.TxnPin })
                .FirstOrDefaultAsync();
            if (actor is null)
            {
                return new WalletTransactionResponse { ErrorMessage = "Admin not found.", IsSuccessful = false };
            }

            if (!string.Equals(request.TxnPin?.Trim(), actor.TxnPin?.Trim(), StringComparison.Ordinal))
            {
                return new WalletTransactionResponse { ErrorMessage = "Invalid Txn Pin", IsSuccessful = false };
            }

            bool isPayout = request.Status == WalletOperationStatus.Credit;
            var debitedId = isPayout ? actor.Id : target.Id;
            var debitedName = isPayout ? $"{actor.Name}-{actor.Phone}" : $"{target.Name}-{target.Phone}";
            var creditedId = isPayout ? target.Id : actor.Id;
            var creditedName = isPayout ? $"{target.Name}-{target.Phone}" : $"{actor.Name}-{actor.Phone}";

            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var txnType = isPayout ? "PARTNER PAYOUT TO DOWNLINE" : "PARTNER COLLECTION FROM DOWNLINE";
                var remarksBase = $"{request.remarks} | {txnType} | Between {actor.Name} ({actor.Phone}) and {target.Name} ({target.Phone})";

                // Debit first, using the same UPDLOCK-protected read WalletService uses everywhere
                // else, so the sufficiency check below is based on the true locked balance (not a
                // stale pre-check) even under concurrent requests for the same wallet.
                var (debitOldBal, debitNewBal, debitEntryId) = await _walletService.DebitAsync(
                    debitedId, debitedName, request.Amount, request.Amount, 0, 0, txnType, remarksBase);

                if (debitNewBal < 0)
                {
                    // Throwing here (before SaveChanges/Commit) rolls back the debit entry too —
                    // guarantees we never leave a debit without its matching credit, or vice versa.
                    throw new InvalidOperationException(
                        isPayout ? "Insufficient balance in your wallet." : $"{target.Name} has insufficient balance.");
                }

                var (creditOldBal, creditNewBal, creditEntryId) = await _walletService.CreditAsync(
                    creditedId, creditedName, request.Amount, request.Amount, 0, 0, txnType, remarksBase);

                var now = DateTime.Now;
                var bankId = Guid.Parse("61A14EEF-9765-45BA-AD22-ADE44D01F708");
                _context.TblPaymentRequest.AddRange(
                    new TblPaymentRequest
                    {
                        PaymentId = Guid.NewGuid(),
                        BankId = bankId,
                        UserId = debitedId,
                        Amount = request.Amount,
                        TxnId = debitEntryId.ToString(),
                        DeposideMode = isPayout ? "Paid to downline" : "Collected from downline",
                        Status = "Approved",
                        CreatedBy = request.ActionById,
                        CreatedOn = now,
                        ModifiedOn = now,
                        IsDeleted = false,
                        UserRemarks = "",
                        AdminRemarks = remarksBase,
                        openingBalance = debitOldBal,
                        closingBalance = debitNewBal
                    },
                    new TblPaymentRequest
                    {
                        PaymentId = Guid.NewGuid(),
                        BankId = bankId,
                        UserId = creditedId,
                        Amount = request.Amount,
                        TxnId = creditEntryId.ToString(),
                        DeposideMode = isPayout ? "Received from partner" : "Refunded to partner",
                        Status = "Approved",
                        CreatedBy = request.ActionById,
                        CreatedOn = now,
                        ModifiedOn = now,
                        IsDeleted = false,
                        UserRemarks = "",
                        AdminRemarks = remarksBase,
                        openingBalance = creditOldBal,
                        closingBalance = creditNewBal
                    });

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                try
                {
                    await _smsService.SendDebitCreditSmsAsync(new DebitCreditSmsRequest
                    {
                        TransferType = "Credit",
                        ReceiverPhone = isPayout ? target.Phone : actor.Phone,
                        ReceiverPreAmount = creditOldBal,
                        ReceiverCurrentAmount = creditNewBal,
                        ReceiverName = isPayout ? target.Name : actor.Name,
                        TransactionAmount = request.Amount
                    });
                }
                catch
                {
                    // SMS is best-effort; never fail a completed, committed transfer because of it.
                }

                return new WalletTransactionResponse
                {
                    Username = target.Username,
                    Oldbalance = (isPayout ? creditOldBal : debitOldBal).ToString("F2"),
                    NewBalance = (isPayout ? creditNewBal : debitNewBal).ToString("F2"),
                    Amount = request.Amount.ToString("F2"),
                    TxnType = txnType,
                    CrdrType = isPayout ? "Credit" : "Debit",
                    Remarks = remarksBase,
                    Txndate = now,
                    ErrorMessage = isPayout ? "Payment sent successfully" : "Amount collected successfully",
                    IsSuccessful = true
                };
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return new WalletTransactionResponse { ErrorMessage = ex.Message, IsSuccessful = false };
            }
        }

        public async Task<bool> IsUserInScopeAsync(int clientId, string scopeType, string scopeId)
        {
            if (clientId <= 0 || string.IsNullOrWhiteSpace(scopeId))
                return false;

            var owner = await _context.TblUsers
                .Where(x => x.Id == clientId)
                .Select(x => new { x.Adid, x.Mdid })
                .FirstOrDefaultAsync();

            if (owner == null)
                return false;

            return string.Equals(scopeType, "AD", StringComparison.OrdinalIgnoreCase)
                ? owner.Adid == scopeId
                : string.Equals(scopeType, "MD", StringComparison.OrdinalIgnoreCase) && owner.Mdid == scopeId;
        }

        private static string ResolveScopePartnerId(CreateOrUpdateClientUserCommand request) =>
            request.ScopePartnerId > 0
                ? request.ScopePartnerId.ToString()
                : Convert.ToString(request.WLID);

        private static ResponseModelforClientUseraddandupdateapi Failure(string message) => new()
        {
            Msg = message,
            flag = false
        };

        private static string? ValidateUpload(IFormFile? file, bool allowPdf)
        {
            if (file == null)
                return null;

            if (file.Length <= 0 || file.Length > 1024 * 1024)
                return $"{file.FileName}: file size must be between 1 byte and 1 MB.";

            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            var allowed = allowPdf
                ? new[] { ".jpg", ".jpeg", ".png", ".pdf" }
                : new[] { ".jpg", ".jpeg", ".png" };

            return allowed.Contains(extension)
                ? null
                : $"{file.FileName}: invalid file type.";
        }
    }
}

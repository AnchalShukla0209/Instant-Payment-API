using InstantPay.Application.Interfaces;
using InstantPay.Application.Interfaces.SMS;
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

namespace InstantPay.Application.Services
{
    public class ClientUserOperation : IClientUserOperation
    {
        private readonly AppDbContext _context;
        private IFileHandler _IFileHandler;
        private readonly AesEncryptionService _aes;
        private readonly ISmsService _smsService;
        private readonly IWalletService _walletService;
        public ClientUserOperation(AppDbContext context, IFileHandler iFileHandler, AesEncryptionService aes, ISmsService smsservice, IWalletService walletService)
        {
            _context = context;
            _IFileHandler = iFileHandler;
            _aes = aes;
            _smsService = smsservice;
            _walletService = walletService;
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

            

            var baseUsers = _context.TblUsers
            .Where(t =>
                t.Wlid == request.ClientId.ToString() &&
                (!fromDate.HasValue || t.RegDate >= fromDate) &&
                (!toDate.HasValue || t.RegDate <= toDate)
            );

            var filteredUserIds = baseUsers.Select(u => (int?)u.Id);

            var filteredLatestBalances =
            from lb in latestBalances
            join u in baseUsers on lb.UserId equals u.Id
            select lb;

            var totalBalance = await filteredLatestBalances.SumAsync(x => x.NewBal ?? 0m);

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


            var totalCount = await baseUsers.CountAsync();

            var usersPaged =
                await (
                    from t1 in baseUsers
                    join cp in _context.Tblcommplans on t1.PlanId equals cp.Id.ToString() into cpj
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
                        PlanName = cp != null ? cp.PlanName + "-" + cp.UserType : "",
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
            TblUser client;
            bool isNew = request.ClientId == 0;

            if (isNew)
            {
                var existingUser = await _context.TblUsers
        .FirstOrDefaultAsync(x => x.Username.ToLower().Trim() == request.UserName.ToLower().Trim());

                if (existingUser != null)
                {
                    return new ResponseModelforClientUseraddandupdateapi
                    {
                        Msg = "Username already exists.",
                        flag = false
                    };
                }
                client = new TblUser
                {
                    Usertype = request.UserType,
                    CompanyName = request.CompanyName,
                    Name = request.CustomerName,
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

                    Status = "Active",

                    RegDate = DateTime.UtcNow,
                    TxnPin = request.TxnPin,
                    PlanId = "1",
                    Wlid = Convert.ToString(request.WLID),
                    MerchargeCode = "",
                    TokenKey = "",
                    Mdid = "0",
                    Adid = "0",
                    DeviceInfo = "",
                    DeviceId = "",
                    Lat = request.lat,
                    Longitute = request.longitute,
                    //MPin = _aes.Encrypt(request.MPin)
                    MPin = (request.MPin)

                };

                _context.TblUsers.Add(client);
                await _context.SaveChangesAsync(cancellationToken);
            }
            else
            {
                client = await _context.TblUsers.FirstOrDefaultAsync(c => c.Id == request.ClientId);
                if (client == null)
                {
                    return new ResponseModelforClientUseraddandupdateapi
                    {
                        Msg = "Record Not Found",
                        flag = false
                    };
                }

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
                client.Status = request.Status;

                client.PlanId = "1";
                client.Wlid = Convert.ToString(request.WLID);
                client.MerchargeCode = "";
                client.TokenKey = "";
                client.Mdid = "0";
                client.Adid = "0";
                client.DeviceInfo = "";
                client.DeviceId = "";
                client.Lat = request.lat;
                client.Longitute = request.longitute;
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
                string filePath = Path.Combine(folderPath, file.FileName);
                using var stream = new FileStream(filePath, FileMode.Create);
                file.CopyTo(stream);
                return Path.Combine("UploadFiles", "ClientUser", client.Id.ToString(), folder, file.FileName).Replace("\\", "/");
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

            await _context.SaveChangesAsync(cancellationToken);

            return new ResponseModelforClientUseraddandupdateapi
            {
                id = client.Id,
                Msg = isNew ? "Client Created Successfully" : "Client Updated",
                flag = true
            };
        }

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
                UserType = t1.Usertype,
                Logo = t1.Logo,
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
                Status = t1.Status,
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

                // ✅ Step 2: Validate admin & TxnPin
                var adminPin = await _context.TblSuperadmins
                    .Where(x => x.Id == request.ActionById)
                    .Select(x => x.TxnPin)
                    .FirstOrDefaultAsync();

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


    }
}

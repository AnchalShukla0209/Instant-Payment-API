using InstantPay.Application.Interfaces;
using InstantPay.Infrastructure.Security;
using InstantPay.Infrastructure.Sql.Entities;
using InstantPay.SharedKernel.Entity;
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

namespace InstantPay.Application.Services
{
    public class ClientUserOperation : IClientUserOperation
    {
        private readonly AppDbContext _context;
        private IFileHandler _IFileHandler;
        private readonly AesEncryptionService _aes;
        public ClientUserOperation(AppDbContext context, IFileHandler iFileHandler, AesEncryptionService aes)
        {
            _context = context;
            _IFileHandler = iFileHandler;
            _aes = aes;
        }

        public async Task<GetClientUsersWithMainBalanceResponse> GetClientUserList(GetClientUserQuery request)
        {
            DateOnly? fromDate = null;
            DateOnly? toDate = null;

            if (DateOnly.TryParse(request.fromDate, out var parsedFromDate))
                fromDate = parsedFromDate;

            if (DateOnly.TryParse(request.toDate, out var parsedToDate))
                toDate = parsedToDate;

            var balanceQuery = _context.Tbluserbalances.AsQueryable();


            if (fromDate.HasValue)
            {
                balanceQuery = balanceQuery.Where(b => b.Txndate.Value.Date >= fromDate.Value.ToDateTime(TimeOnly.MinValue).Date);
            }
            if (toDate.HasValue)
                balanceQuery = balanceQuery.Where(b => b.Txndate.Value.Date <= toDate.Value.ToDateTime(TimeOnly.MinValue).Date);

            // Step 1: Get latest balances by UserId + UserName from DB
            var latestBalances = await balanceQuery
                .GroupBy(b => new { b.UserId, b.UserName })
                .Select(g => g.OrderByDescending(b => b.Id).FirstOrDefault())
                .ToListAsync(); // Materialize here so EF is done

            // Step 2: Build dictionary in memory using tuple key, normalize username
            var balanceDict = latestBalances
            .ToDictionary(
                b => (b.UserId, (b.UserName ?? string.Empty).Trim().ToLowerInvariant()),
                b => b.NewBal ?? 0m // if null, store as 0
            );


            var totalBalance = balanceDict.Values.Sum();

            // Step 4: Total count
            var totalCount = await _context.TblUsers
                .Where(t1 => t1.Wlid != null && t1.Wlid == request.ClientId.ToString())
                .CountAsync();

            // Step 5: Main query
            var users = await
                (from t1 in _context.TblUsers
                 join cp in _context.Tblcommplans on t1.PlanId equals Convert.ToString(cp.Id) into cpj
                 from cp in cpj.DefaultIfEmpty()
                 join t2 in _context.TblUsers on t1.Adid equals Convert.ToString(t2.Id) into adJ
                 from t2 in adJ.DefaultIfEmpty()
                 join t3 in _context.TblUsers on t1.Mdid equals Convert.ToString(t3.Id) into mdJ
                 from t3 in mdJ.DefaultIfEmpty()
                 where t1.Wlid != null && t1.Wlid == request.ClientId.ToString()
                 orderby t1.Id descending
                 select new
                 {
                     t1.Id,
                     UserName = t1.Username ?? string.Empty,
                     t1.CompanyName,
                     t1.City,
                     t1.Usertype,
                     t1.Phone,
                     t1.Status,
                     t1.EmailId,
                     PlanName = cp != null
                         ? (cp.PlanName + "-" + cp.UserType)
                         : string.Empty,
                     ADName = t2 != null ? t2.Name : "NA",
                     MDName = t3 != null ? t3.Name : "NA",
                     CreatedDate = t1.RegDate
                 })
                .Skip((request.pageIndex - 1) * request.pageSize)
                .Take(request.pageSize)
                .AsNoTracking()
                .ToListAsync();

            // Step 6: Map + inject balances from dictionary
            var result = users.Select(u =>
            {
                var lookupKey = (u.Id, (u.UserName ?? string.Empty).Trim().ToLowerInvariant());
                return new UserBalanceRec
                {
                    Id = u.Id,
                    UserName = u.UserName,
                    UserType = u.Usertype,
                    Phone = u.Phone,
                    CompanyName = u.CompanyName ?? string.Empty,
                    City = u.City ?? string.Empty,
                    Status = u.Status ?? string.Empty,
                    EmailId = u.EmailId ?? string.Empty,
                    PlanName = u.PlanName,
                    ADName = u.ADName,
                    MDName = u.MDName,
                    CreatedDate = (DateTime)u.CreatedDate,
                    MainBalance = balanceDict.TryGetValue(lookupKey, out var bal) ? bal : 0m
                };
            }).ToList();



            return new GetClientUsersWithMainBalanceResponse
            {
                PageIndex = request.pageIndex,
                PageSize = request.pageSize,
                TotalRecords = totalCount,
                TotalBalance = (decimal)totalBalance,
                Users = result
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
                    Password = _aes.Encrypt(request.Password),
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
                    Longitute = request.longitute

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
                client.Password = _aes.Encrypt(request.Password);
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
                return Path.Combine("UploadFiles","ClientUser", client.Id.ToString(), folder, file.FileName).Replace("\\", "/");
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

                // Join AD user
            join t2 in _context.TblUsers on t1.Adid equals Convert.ToString(t2.Id) into adJ
            from t2 in adJ.DefaultIfEmpty()

                // Join MD user
            join t3 in _context.TblUsers on t1.Mdid equals Convert.ToString(t3.Id) into mdJ
            from t3 in mdJ.DefaultIfEmpty()

                // Join SuperAdmin using WlId from TblUsers
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
            Password = _aes.Decrypt(t1.Password),
            PanCard = t1.PanCard,
            AadharCard = t1.AadharCard,
            CustomerName = t1.Name,
            UserType = t1.Usertype,
            Logo = t1.Logo,
            AddressLine1 = t1.AddressLine1,
            AddressLine2 = t1.AddressLine2,
            State = t1.State,
            City = t1.City,

                // Names from joined tables
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
            ClientId = t1.Id
            
            }
            ).FirstOrDefaultAsync();



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
            var dto = request;
            using var transaction = await _context.Database.BeginTransactionAsync();
            {
                try
                {
                    var user = await _context.TblUsers
                        .Where(x => x.Id == dto.UserId)
                        .Select(x => new { x.Id, x.Username, x.Phone })
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

                    var oldBalance = await _context.Tbluserbalances
                        .Where(b => Convert.ToInt32(b.UserId) == dto.UserId && b.UserName.Trim().ToLower() == user.Username.Trim().ToLower())
                        .OrderByDescending(b => b.Id)
                        .Select(b => b.NewBal)
                        .FirstOrDefaultAsync();

                    var newBalance = dto.Status == WalletOperationStatus.Credit ? (oldBalance ?? 0) + dto.Amount : (oldBalance ?? 0) - dto.Amount;
                    var txnType = dto.Status == WalletOperationStatus.Credit ? "WALLET TOPUP BY ADMIN" : "WALLET DEBIT BY ADMIN";
                    var remarks = $"{txnType} For Account No {user.Phone} | {(dto.Status == WalletOperationStatus.Credit ? "Credit" : "Debit")} by Services | Wallet {(dto.Status == WalletOperationStatus.Credit ? "TopUp" : "Debit")} BY Admin Account";

                    _context.Tbluserbalances.Add(new Tbluserbalance
                    {
                        TxnAmount = dto.Amount,
                        SurCom = 0,
                        Tds = 0,
                        UserId = dto.UserId,
                        UserName = user.Username,
                        OldBal = oldBalance,
                        Amount = dto.Amount,
                        NewBal = newBalance,
                        TxnType = txnType,
                        CrdrType = dto.Status == WalletOperationStatus.Credit ? "Credit" : "Debit",
                        Remarks = remarks,
                        Txndate = DateTime.Now
                    });

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();
                    return new WalletTransactionResponse
                    {
                        Username = user.Username,
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

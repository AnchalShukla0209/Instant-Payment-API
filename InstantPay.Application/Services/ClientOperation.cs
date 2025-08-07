using InstantPay.Application.Interfaces;
using InstantPay.Infrastructure.Sql.Entities;
using InstantPay.SharedKernel.Entity;
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
        public ClientOperation(AppDbContext context, IFileHandler iFileHandler)
        {
            _context = context;
            _IFileHandler = iFileHandler;
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

            var latestBalances = await balanceQuery
                .GroupBy(b => b.UserId)
                .Select(g => g.OrderByDescending(b => b.Id).FirstOrDefault())
                .ToListAsync();

            var balanceDict = latestBalances.ToDictionary(b => b.UserId, b => b.NewBal);
            var totalBalance = balanceDict.Values.Sum();

            var totalCount = await _context.TblWlUsers.CountAsync();

            var users = await _context.TblWlUsers
                .OrderByDescending(u => u.Id)
                .Skip((request.pageIndex - 1) * request.pageSize)
                .Take(request.pageSize)
                .Select(u => new UserBalanceDto
                {
                    Id = u.Id,
                    UserName = u.UserName ?? "",
                    CompanyName = u.CompanyName ?? "",
                    Domain = u.DomainName ?? "",
                    City = u.City ?? "",
                    Status = u.Status ?? "",
                    EmailId = u.EmailId ?? "",
                    MainBalance = (decimal)(balanceDict.ContainsKey(Convert.ToString(u.Id)) ? balanceDict[Convert.ToString(u.Id)] : 0)
                })
                .ToListAsync();

            return new GetUsersWithMainBalanceResponse
            {
                PageIndex = request.pageIndex,
                PageSize = request.pageSize,
                TotalRecords = totalCount,
                TotalBalance = (decimal)totalBalance,
                Users = users
            };
        }

        public async Task<ResponseModelforClientaddandupdateapi> CreateOrUpdateClient(CreateOrUpdateClientCommand request, CancellationToken cancellationToken)
        {
            TblWlUser client;
            bool isNew = request.ClientId == 0;

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
                    Password = request.Password,
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
                    Status = "Active",
                    RegDate = DateTime.UtcNow,
                    TxnPin = request.TxnPin,
                    PlanId = request.PlanId,
                };

                _context.TblWlUsers.Add(client);
                await _context.SaveChangesAsync(cancellationToken);
            }
            else
            {
                client = await _context.TblWlUsers.FirstOrDefaultAsync(c => c.Id == request.ClientId);
                if (client == null)
                {
                    return new ResponseModelforClientaddandupdateapi
                    {
                        Msg = "Record Not Found",
                        flag = false
                    };
                }

                var existingUser = await _context.TblWlUsers
        .FirstOrDefaultAsync(x => x.UserName.ToLower().Trim() == request.UserName.ToLower().Trim() && x.Id != request.ClientId);

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
                client.Password = request.Password;
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
                client.Status = request.Status;
                client.TxnPin = request.TxnPin;
            }

            string webRootPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
            string basePath = Path.Combine(webRootPath, "UploadFiles", client.Id.ToString());
            Directory.CreateDirectory(basePath);
            string? SaveFile(IFormFile file, string folder)
            {
                if (file == null) return null;
                string folderPath = Path.Combine(basePath, folder);
                Directory.CreateDirectory(folderPath);
                string filePath = Path.Combine(folderPath, file.FileName);
                using var stream = new FileStream(filePath, FileMode.Create);
                file.CopyTo(stream);
                return Path.Combine("UploadFiles", client.Id.ToString(), folder, file.FileName).Replace("\\", "/");
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
            if (request.AadharFrontFile != null )
            {
                 aadharPath = SaveFile(request.AadharFrontFile, "AadharCard");
                 client.AadharFront = aadharPath ?? "";
            }
            if (request.AadharBackFile != null )
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
                    Password = c.Password,
                    PanCard = c.PanCard,
                    AadharCard = c.AadharCard,
                    DomainName = c.DomainName,
                    Logo = c.Logo,
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
                    Status = c.Status,
                    RegDate = c.RegDate,
                    TxnPin = c.TxnPin,
                    PlanId = c.PlanId
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

                    var newBalance = dto.Status == WalletOperationStatus.Credit
    ? (oldBalance ?? 0) + dto.Amount
    : (oldBalance ?? 0) - dto.Amount;


                    var txnType = dto.Status == WalletOperationStatus.Credit ? "WALLET TOPUP BY ADMIN" : "WALLET DEBIT BY ADMIN";
                    var remarks = $"{txnType} For Account No {user.Phone} | {(dto.Status == WalletOperationStatus.Credit ? "Credit" : "Debit")} by Services | Wallet {(dto.Status == WalletOperationStatus.Credit ? "TopUp" : "Debit")} BY Admin Account";

                    _context.TblWlbalances.Add(new TblWlbalance
                    {
                        TxnAmount = dto.Amount,
                        SurComm = 0,
                        Tds = 0,
                        UserId = Convert.ToString(dto.UserId),
                        UserName = user.UserName,
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

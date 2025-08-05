using InstantPay.Application.Interfaces;
using InstantPay.Infrastructure.Sql.Entities;
using InstantPay.SharedKernel.Entity;
using Microsoft.EntityFrameworkCore;
using MongoDB.Bson.IO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace InstantPay.Application.Services
{
    public class ClientOperation : IClientOperation
    {
        private readonly AppDbContext _context;
        public ClientOperation(AppDbContext context)
        {
            _context = context;
        }

        public async Task<GetUsersWithMainBalanceResponse> GetClientList(GetUsersWithMainBalanceQuery request)
        {
            DateOnly? fromDate = null;
            DateOnly? toDate = null;

            if (DateOnly.TryParse(request.FromDate, out var parsedFromDate))
                fromDate = parsedFromDate;

            if (DateOnly.TryParse(request.ToDate, out var parsedToDate))
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
                .Skip((request.PageIndex - 1) * request.PageSize)
                .Take(request.PageSize)
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
                PageIndex = request.PageIndex,
                PageSize = request.PageSize,
                TotalRecords = totalCount,
                TotalBalance = (decimal)totalBalance,
                Users = users
            };
        }


        public async Task<ResponseModelforClientaddandupdateapi> CreateOrUpdateClient(CreateOrUpdateClientCommand request, CancellationToken cancellationToken)
        {
            //string? panPath = request.PanCardFile != null ? await _fileStorage.SaveAsync(request.PanCardFile) : null;
            //string? aadharPath = request.AadharCardFile != null ? await _fileStorage.SaveAsync(request.AadharCardFile) : null;
            //string? profilePath = request.ProfileFile != null ? await _fileStorage.SaveAsync(request.ProfileFile) : null;
            //string? otherPath = request.OtherFile != null ? await _fileStorage.SaveAsync(request.OtherFile) : null;

            TblWlUser client;

            if (request.ClientId == 0)
            {
                client = new TblWlUser
                {
                    CompanyName = request.CompanyName,
                    UserName = request.UserName,
                    EmailId = request.EmailId,
                    Phone = request.Phone,
                    Password = request.Password,
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
                    Logo = Newtonsoft.Json.JsonConvert.SerializeObject(new
                    {
                        //PanCard = panPath,
                        //AadharCard = aadharPath,
                        //Profile = profilePath,
                        //Other = otherPath
                    })
                };

                _context.TblWlUsers.Add(client);
                object value = await _context.SaveChangesAsync(cancellationToken);

                return new ResponseModelforClientaddandupdateapi
                {
                    id = client.Id,
                    Msg = "Client Created Successfully",
                    flag = true
                };
            }
            else
            {
                client = await _context.TblWlUsers.FirstOrDefaultAsync(c => c.Id == request.ClientId);
                if (client == null)
                {
                    return new ResponseModelforClientaddandupdateapi
                    {
                        Msg = "Client Not Found",
                        flag = false
                    };
                }

                client.CompanyName = request.CompanyName;
                client.UserName = request.UserName;
                client.EmailId = request.EmailId;
                client.Phone = request.Phone;
                client.Password = request.Password;
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

                // Update file paths only if newly uploaded
                var currentLogoData = Newtonsoft.Json.JsonConvert.DeserializeObject<dynamic>(client.Logo ?? "{}");
                client.Logo = Newtonsoft.Json.JsonConvert.SerializeObject(new
                {
                    //PanCard = panPath ?? currentLogoData?.PanCard,
                    //AadharCard = aadharPath ?? currentLogoData?.AadharCard,
                    //Profile = profilePath ?? currentLogoData?.Profile,
                    //Other = otherPath ?? currentLogoData?.Other
                });

                await _context.SaveChangesAsync(cancellationToken);

                return new ResponseModelforClientaddandupdateapi
                {
                    id = client.Id,
                    Msg = "Client Updated",
                    flag = true
                };
            }
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
    }
}

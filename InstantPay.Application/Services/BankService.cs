using InstantPay.Application.Interfaces;
using InstantPay.Infrastructure.Security;
using InstantPay.Infrastructure.Sql.Entities;
using InstantPay.SharedKernel.Entity;
using InstantPay.SharedKernel.Results;
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
    public class BankService : IBankRepository
    {
        private readonly AppDbContext _context;

        public BankService(AppDbContext context) => _context = context;

        public async Task<PagedResult<BankDto>> GetAllAsync(int pageNumber, int pageSize)
        {
            var query = _context.BankMaster.Where(x => !x.IsDeleted);
            var total = await query.CountAsync();
            var data = await query
                .OrderBy(x => x.BankName)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .Select(x => new BankDto
                {
                    BankId = x.BankId,
                    BankName = x.BankName,
                    AccountNumber = x.AccountNumber,
                    IFSCCode = x.IFSCCode,
                    PhoneNo = x.PhoneNo,
                    TxnCharge = x.TxnCharge,
                    IsActive = x.IsActive
                }).ToListAsync();

            return new PagedResult<BankDto>(data, total, pageNumber, pageSize);
        }

        public async Task<List<BankDto>> GetAllActiveAsync()
        {
            return await _context.BankMaster
                .Where(x => x.IsActive && !x.IsDeleted)
                .Select(x => new BankDto { BankId = x.BankId, BankName = x.BankName })
                .ToListAsync();
        }

        public async Task<BankDto> GetByIdAsync(Guid bankId)
        {
            return await _context.BankMaster
                .Where(x => x.BankId == bankId && !x.IsDeleted)
                .Select(x => new BankDto
                {
                    BankId = x.BankId,
                    BankName = x.BankName,
                    AccountNumber = x.AccountNumber,
                    IFSCCode = x.IFSCCode,
                    PhoneNo = x.PhoneNo,
                    TxnCharge = (decimal)x.TxnCharge,
                    IsActive = x.IsActive
                }).FirstOrDefaultAsync();
        }

        public async Task<Guid> CreateAsync(BankDto bank, int CreatedBy)
        {
            if (await ExistsByNameAsync(bank.BankName))
                throw new Exception("Duplicate Bank Name not allowed.");

            var entity = new BankMaster
            {
                BankId = Guid.NewGuid(),
                BankName = bank.BankName,
                AccountNumber = bank.AccountNumber,
                IFSCCode = bank.IFSCCode,
                PhoneNo = bank.PhoneNo,
                TxnCharge = bank.TxnCharge,
                IsActive = bank.IsActive,
                CreatedBy = 1,
                CreatedOn = DateTime.UtcNow
            };
            _context.BankMaster.Add(entity);
            await _context.SaveChangesAsync();
            return entity.BankId;
        }

        public async Task UpdateAsync(BankDto bank, int ModifiedBy)
        {
            if (await ExistsByNameAsync(bank.BankName, bank.BankId))
                throw new Exception("Duplicate Bank Name not allowed.");

            var entity = await _context.BankMaster.FindAsync(bank.BankId);
            if (entity == null || entity.IsDeleted)
                throw new Exception("Bank not found.");

            entity.BankName = bank.BankName;
            entity.AccountNumber = bank.AccountNumber;
            entity.IFSCCode = bank.IFSCCode;
            entity.PhoneNo = bank.PhoneNo;
            entity.TxnCharge = bank.TxnCharge;
            entity.IsActive = bank.IsActive;
            entity.ModifiedBy = ModifiedBy;
            entity.ModifiedOn = DateTime.UtcNow;

            await _context.SaveChangesAsync();
        }

        public async Task DeleteAsync(Guid bankId, int ModifiedBy)
        {
            var entity = await _context.BankMaster.FindAsync(bankId);
            if (entity == null)
                throw new Exception("Bank not found.");

            entity.IsDeleted = true;
            entity.ModifiedBy = ModifiedBy;
            entity.ModifiedOn = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }

        public async Task<bool> ExistsByNameAsync(string bankName, Guid? excludeId = null)
        {
            return await _context.BankMaster
                .AnyAsync(x => x.BankName == bankName && !x.IsDeleted &&
                               (!excludeId.HasValue || x.BankId != excludeId.Value));
        }

        public async Task<List<BankDropdownDto>> GetBankListForJPB()
        {
            return await _context.JPBBankMasters
                .Where(x => x.IsActive)
                .AsNoTracking()
                .OrderBy(x => x.IssuerBankName)
                .Select(x => new BankDropdownDto
                {
                    bankName = x.IssuerBankName,
                    nbin = x.IIN
                })
                .ToListAsync();
        }

    }

}

using InstantPay.SharedKernel.Entity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InstantPay.Application.Interfaces
{
    public interface IBankRepository
    {
        Task<PagedResult<BankDto>> GetAllAsync(int pageNumber, int pageSize);
        Task<List<BankDto>> GetAllActiveAsync();
        Task<BankDto> GetByIdAsync(Guid bankId);
        Task<Guid> CreateAsync(BankDto bank);
        Task UpdateAsync(BankDto bank);
        Task DeleteAsync(Guid bankId);
        Task<bool> ExistsByNameAsync(string bankName, Guid? excludeId = null);
    }
}

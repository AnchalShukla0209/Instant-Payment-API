using InstantPay.SharedKernel.Entity;
using InstantPay.SharedKernel.Results;
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
        Task<Guid> CreateAsync(BankDto bank, int CreatedBy);
        Task UpdateAsync(BankDto bank, int ModifiedBy);
        Task DeleteAsync(Guid bankId, int ModifiedBy);
        Task<bool> ExistsByNameAsync(string bankName, Guid? excludeId = null);
        Task<List<BankDropdownDto>> GetBankListForJPB();
    }
}

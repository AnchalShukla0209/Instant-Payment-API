using InstantPay.Infrastructure.Sql.Entities;
using InstantPay.SharedKernel.Entity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InstantPay.Application.Interfaces
{
    public interface IServiceService
    {
        Task<PageResult<Tbl_Services>> GetAllAsync(int pageIndex = 1, int pageSize = 10);
        Task<Tbl_Services> GetByIdAsync(int id);
        Task<string> CreateAsync(ServiceDtoRequest dto);
        Task<string> UpdateAsync(int id, ServiceDtoRequest dto);
        Task<string> DeleteAsync(int id);
    }

}

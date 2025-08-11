using InstantPay.SharedKernel.Entity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InstantPay.Application.Interfaces
{
    public interface ISlabReadRepository
    {
        Task<PagedResult<SlabInfoDto>> GetSlabInfoAsync(string? serviceName, int pageIndex, int pageSize);
        Task<UpdateCommissionResult> Handle(UpdateCommissionCommand request);
    }
}

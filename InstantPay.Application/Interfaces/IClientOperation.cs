using InstantPay.SharedKernel.Entity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InstantPay.Application.Interfaces
{
    public interface IClientOperation
    {
        Task<GetUsersWithMainBalanceResponse> GetClientList(GetUsersWithMainBalanceQuery request);
        Task<ResponseModelforClientaddandupdateapi> CreateOrUpdateClient(CreateOrUpdateClientCommand request, CancellationToken cancellationToken);
    }
}

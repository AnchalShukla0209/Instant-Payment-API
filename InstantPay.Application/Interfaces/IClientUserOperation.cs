using InstantPay.SharedKernel.Entity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InstantPay.Application.Interfaces
{
    public interface IClientUserOperation
    {
        Task<GetClientUsersWithMainBalanceResponse> GetClientUserList(GetClientUserQuery request);
        Task<ResponseModelforClientUseraddandupdateapi> CreateOrUpdateClientUser(CreateOrUpdateClientUserCommand request, CancellationToken cancellationToken);
        Task<GetClientUserDetail?> GetClientUserDetailByIdAsync(int Id);
        Task<ResponseModelforClientUseraddandupdateapi> HandleDeleteClientUserFile(DeleteClientUserFileCommand request, CancellationToken cancellationToken);

        Task<WalletTransactionResponse> AddWalletToClientUser(WalletTransactionRequest request);

    }
}

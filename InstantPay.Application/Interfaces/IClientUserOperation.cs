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

        /// <summary>
        /// Peer-to-peer wallet transfer between a Distributor/Master Distributor (ActionById) and one of
        /// their downline users (UserId). Unlike <see cref="AddWalletToClientUser"/> (which is a one-sided
        /// WL-Admin top-up with no counterparty), this always moves money between the two wallets in a single
        /// atomic transaction: Credit = ActionById debited / UserId credited, Debit = UserId debited /
        /// ActionById credited. Either both sides succeed or neither is written.
        /// </summary>
        Task<WalletTransactionResponse> TransferWalletForPartnerAsync(WalletTransactionRequest request);

        /// <summary>Checks whether the given client user belongs to the AD/MD scope identified by scopeType + scopeId.</summary>
        Task<bool> IsUserInScopeAsync(int clientId, string scopeType, string scopeId);

    }
}

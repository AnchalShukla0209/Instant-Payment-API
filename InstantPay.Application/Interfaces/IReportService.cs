using InstantPay.Application.DTOs;
using InstantPay.SharedKernel.Entity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InstantPay.Application.Interfaces
{
    public interface IReportService
    {
        Task<PaginatedTxnResultDto> GetTransactionReportAsync(
    string serviceType, string status, string dateFrom, string dateTo,
    int userId, int pageIndex = 1, int pageSize = 50, string commonsearch = "", int ispaginationenabled = 1);

        Task<TxnDetailsData> GetTxnDetails(int txnId, string ServiceName);

        Task<TxnUpdateResponse> UpdateTxnStatus(TxnUpdateRequest request, int actionById);

        Task<PaginatedTxnResultDto> GetUserTransactionReportAsync(
   string serviceType, string status, string dateFrom, string dateTo,
   int userId, string username, int pageIndex = 1, int pageSize = 50, string commonsearch = "", int ispaginationenabled = 1);

        /// <summary>
        /// Distributor / Master Distributor downline transaction report - same column set and
        /// service-type branches as <see cref="GetUserTransactionReportAsync"/>, but scoped to
        /// every user under the partner's network (TblUsers.Adid/Mdid == partnerId) plus the
        /// partner's own transactions, instead of a single retailer's own transactions.
        /// </summary>
        Task<PaginatedTxnResultDto> GetPartnerTransactionReportAsync(
   int partnerId, string userType, string serviceType, string status, string dateFrom, string dateTo,
   int pageIndex = 1, int pageSize = 50, string commonsearch = "", int ispaginationenabled = 1, int filterUserId = 0);

        /// <summary>Downline users (Adid/Mdid == partnerId) for the partner transaction report's "User" dropdown.</summary>
        Task<List<PartnerUserDropdownDto>> GetPartnerUserDropdownAsync(int partnerId, string userType);
    }
}

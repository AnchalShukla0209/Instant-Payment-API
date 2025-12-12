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
    int userId, int pageIndex = 1, int pageSize = 50);

        Task<TxnDetailsData> GetTxnDetails(int txnId);

        Task<TxnUpdateResponse> UpdateTxnStatus(TxnUpdateRequest request, int actionById);

        Task<PaginatedTxnResultDto> GetUserTransactionReportAsync(
   string serviceType, string status, string dateFrom, string dateTo,
   int userId, string username, int pageIndex = 1, int pageSize = 50);
    }
}

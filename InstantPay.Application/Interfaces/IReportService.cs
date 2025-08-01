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
    }
}

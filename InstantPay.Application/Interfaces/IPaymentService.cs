using InstantPay.SharedKernel.Entity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InstantPay.Application.Interfaces
{
    public interface IPaymentService
    {
        Task<Guid> CreatePaymentRequestAsync(PaymentRequestDto request, int userId);
        Task UpdatePaymentAsync(PaymentUpdateDto request);
        Task<(IEnumerable<PaymentResponseDto> Payments, int TotalCount)>
        GetAllPaymentsAsync(int pageNumber, int pageSize, string status, DateTime? fromDate, DateTime? toDate);
        Task<(byte[] FileContent, string FileName, string ContentType)> DownloadTxnSlipAsync(Guid paymentId);
        Task<PaymentResponseDto> GetPaymentByIdAsync(Guid paymentId);
    }
}

using InstantPay.SharedKernel.RequestPayload.RazorPay;
using InstantPay.SharedKernel.Results.RazorPay;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InstantPay.Application.Interfaces.RazorPay
{
    public interface IRazorpayService
    {
        Task<CreateOrderResponse> CreateOrder(CreateOrderRequest request);
        Task<bool> VerifyPayment(string paymentId, string orderId, string signature);
    }
}

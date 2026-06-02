using InstantPay.Application.Interfaces;
using InstantPay.Application.Interfaces.RazorPay;
using InstantPay.Infrastructure.Sql.Entities;
using InstantPay.SharedKernel.RequestPayload.RazorPay;
using InstantPay.SharedKernel.Results.RazorPay;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Identity.Client;
using Razorpay.Api;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace InstantPay.Application.Services.RazorPay
{
    public class RazorpayService : IRazorpayService
    {
        private readonly IPaymentRepository _repo;
        private readonly AppDbContext _context;
        private readonly IConfiguration _config;

        public RazorpayService(IPaymentRepository repo, IConfiguration config, AppDbContext context)
        {
            _repo = repo;
            _config = config;
            _context = context;
        }

        public async Task<CreateOrderResponse> CreateOrder(CreateOrderRequest request)
        {
            if (request.Amount < 100 || request.Amount > 100000)
                throw new ArgumentException("Invalid amount");

            string orderId = DateTime.Now.ToString("yyyyMMddHHmmssfff");

            var key = _config["Razorpay:Key"];
            var secret = _config["Razorpay:Secret"];

            RazorpayClient client = new RazorpayClient(key, secret);

            var options = new Dictionary<string, object>
            {
                { "amount", request.Amount * 100 },
                { "currency", "INR" },
                { "receipt", orderId },
                { "payment_capture", 1 }
            };

            var userData = _context.TblUsers.Where(x => x.Id == Convert.ToInt32(request.UserId)).FirstOrDefault();
            if (userData == null)
            {
                return new CreateOrderResponse
                {
                    success = false,
                    OrderId = "",
                    Amount = request.Amount,
                    Key =  "Invalid User!"
                };
            }

            var order = client.Order.Create(options);

            int chargeId = string.Equals(request.PaymentMethod, "card", StringComparison.OrdinalIgnoreCase) ? 1 : 24;
            var configuredCharge = _context.Tblpaymentcharges.Where(x => x.Id == chargeId).FirstOrDefault();

            decimal charge = request.Amount * (decimal)configuredCharge.Charge / 100;

            decimal gst = charge * 0.18m;
            decimal transfer = request.Amount - (charge + gst);

            await _repo.Insert(new Tblonlinepayment
            {
                OrderId = orderId,
                TxnId = order["id"].ToString(),
                Amount = request.Amount,
                TxnCharge = charge,
                Gst = gst,
                TransferAmt = transfer,
                Status = "Pending",
                UserKey = Convert.ToString(request.UserId),
                MobileNo = request.Mobile,
                Pancard = request.Pan,
                AadharCard = request.Aadhar,
                PanName = request.Name,
                ReqDate = DateTime.Now,
                ReqBy = request.comingfrom??"Web",
                WlId = userData.Wlid,
                Mdid = userData.Mdid,
                AdId = userData.Adid,
                UserName = userData.Name+"-"+userData.Phone,
                Gatwaytype = "Razorpay",
                Reqlogs = "Razorpay",

            });

            return new CreateOrderResponse
            {
                success = true,
                OrderId = order["id"].ToString(),
                Amount = request.Amount,
                Key = _config["Razorpay:Key"]
            };
        }

        public async Task<bool> VerifyPayment(string paymentId, string orderId, string signature)
        {
            try
            {
                var attributes = new Dictionary<string, string>
                {
                    { "razorpay_payment_id", paymentId },
                    { "razorpay_order_id", orderId },
                    { "razorpay_signature", signature }
                };

                Utils.verifyPaymentSignature(attributes);

                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}

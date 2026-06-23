using InstantPay.Application.Interfaces;
using InstantPay.Infrastructure.Sql.Entities;
using InstantPay.SharedKernel.RequestPayload.RazorPay;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Razorpay.Api;

namespace InstantPay.API.Controller
{

    [ApiController]
    [Route("api/razorpay")]
    [AllowAnonymous]
    public class RazorpayWebhookController : ControllerBase
    {
        private readonly IConfiguration _config;
        private readonly AppDbContext _context;
        private readonly IWalletService _walletService;
        public RazorpayWebhookController(IConfiguration config, AppDbContext context, IWalletService walletService)
        {
            _config = config;
            _context = context;
            _walletService = walletService;
        }


        [HttpPost("webhook")]
        public async Task<IActionResult> HandleWebhook()
        {
            string body = "";
            try
            {
                using var reader = new StreamReader(Request.Body);
                body = await reader.ReadToEndAsync();

                await Log("RAW REQUEST", body, "");
                var signature = Request.Headers["X-Razorpay-Signature"].ToString();
                var secret = _config["Razorpay:WebhookSecret"];

                try
                {
                    Utils.verifyWebhookSignature(body, signature, secret);
                }
                catch (Exception ex)
                {
                    await Log("SIGNATURE FAILED", body, ex.Message);
                    return Ok();
                }
                RazorpayWebhookRequest webhook;

                if (body.Contains("\"payload\":\""))
                {
                    var outer = JsonConvert.DeserializeObject<dynamic>(body);
                    var actualJson = outer.payload.ToString();
                    webhook = JsonConvert.DeserializeObject<RazorpayWebhookRequest>(actualJson);
                }
                else
                {
                    webhook = JsonConvert.DeserializeObject<RazorpayWebhookRequest>(body);
                }
                var payment = webhook?.Payload?.Payment?.Entity;
                if (payment == null)
                {
                    await Log("INVALID PAYMENT OBJECT", body, "");
                    return Ok();
                }

                string eventType = webhook.Event;
                string orderId = payment.Order_Id;
                string paymentId = payment.Id;
                string status = payment.Status;
                string method = payment.Method;
                string last4 = payment.Card?.Last4 ?? "";
                string network = payment.Card?.Network ?? "unknown";

                string rrn = payment.Acquirer_Data?.Rrn ?? "";
                string authCode = payment.Acquirer_Data?.Auth_Code ?? "";
                var txn = await _context.Tblonlinepayments
                    .FirstOrDefaultAsync(x => x.TxnId.Trim().ToLower() == orderId.Trim().ToLower());

                if (txn == null)
                {
                    await Log("TXN NOT FOUND", body, orderId);
                    return Ok();
                }

                var userData = await _context.TblUsers
                    .FirstOrDefaultAsync(x => x.Id == Convert.ToInt32(txn.UserKey));

                if (eventType == "payment.captured")
                {
                    txn.Status = "Success";
                    txn.Paymentid = paymentId;
                    txn.Apiresponse = body;
                    txn.Cardno = last4;
                    txn.Cardtype = network;
                    txn.ResDate = DateTime.Now;
                    txn.Rrn = rrn;

                    await _walletService.CreditAsync(
                        Convert.ToInt32(txn.UserKey),
                        (userData?.Name + "-" + userData?.Phone),
                        txn.Amount ?? 0, txn.TransferAmt ?? 0,
                        txn.TxnCharge + txn.Gst ?? 0, 0,
                        "CREDIT BY PAYMENT GATEWAY",
                        $"Credited | OrderId: {txn.OrderId} | RRN: {rrn}",
                        userData?.Wlid);
                }
                else if (eventType == "payment.failed")
                {
                    txn.Status = "Failed";
                }

                await _context.SaveChangesAsync();

                return Ok();
            }
            catch (Exception ex)
            {
                await Log("MAIN ERROR", body, ex.Message);
                return Ok(); 
            }
        }

        private async Task Log(string title, string request, string response)
        {
            await _context.Apilogs.AddAsync(new Apilog
            {
                Apiname = title,
                Reqdatae = DateTime.Now,
                Request = request,
                Response = response
            });

            await _context.SaveChangesAsync();
        }
    }
}


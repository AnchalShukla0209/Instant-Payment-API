using InstantPay.Application.Interfaces.SMS;
using InstantPay.SharedKernel.RequestPayload.DebitCredit;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.Json;
using InstantPay.Infrastructure.Sql.Entities;

namespace InstantPay.Application.Services.SMS
{
    public class SmsService : ISmsService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _config;
        private readonly AppDbContext _context;

        public SmsService(HttpClient httpClient, IConfiguration config, AppDbContext dbcontext)
        {
            _httpClient = httpClient;
            _config = config;
            _context = dbcontext;
        }

        public async Task<bool> SendDebitCreditSmsAsync(DebitCreditSmsRequest data)
        {
            try
            {
                var senderId = _config["MSG91:SenderId"];
                var authKey = _config["MSG91:AuthKey"];

                var message = $"Dear {data.ReceiverName}, {data.TransactionAmount} " +
                              $"{data.TransferType} {(data.TransferType == "Credit" ? "to" : "from")} your account. " +
                              $"Available Balance {data.ReceiverCurrentAmount}. team instantpayment";

                var body = new
                {
                    sender = senderId.ToLower(),
                    route = "4",
                    country = "91",
                    DLT_TE_ID = "1207162766863535520",
                    sms = new[]
                    {
                        new
                        {
                            message = message,
                            to = new[] { data.ReceiverPhone }
                        }
                }
                };

                var json = System.Text.Json.JsonSerializer.Serialize(body);
                var request = new HttpRequestMessage(HttpMethod.Post, "https://api.msg91.com/api/v2/sendsms");
                request.Headers.Add("authkey", authKey);
                var content = new StringContent(json, Encoding.UTF8);
                content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
                request.Content = content;
                var response = await _httpClient.SendAsync(request);
                var responseContent = await response.Content.ReadAsStringAsync();
                var logdetails = new Apilog
                {
                    Apiname = "SEndSMS",
                    Reqdatae = DateTime.Now,
                    Request = json,
                    Response = responseContent
                };
                _context.Apilogs.Add(logdetails);
                await _context.SaveChangesAsync();
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InstantPay.Application.Interfaces
{
    public interface IEmailService
    {
        Task <string> SendOtpEmailAsync(string toEmail, string body);
        Task<string> SendClientUserVerificationOtpAsync(string toEmail, string otp);
        Task<string> SendSettlementStatusEmailAsync(string transactionId, string accountNo, string txnType, decimal amount, string userName, DateTime createdOn, string status, string rrn = null);
        Task<string> SendTransactionStatusEmailAsync(string transactionId, string accountNo, string txnType, decimal amount, string userName, DateTime createdOn, string status, string rrn = null);
    }
}

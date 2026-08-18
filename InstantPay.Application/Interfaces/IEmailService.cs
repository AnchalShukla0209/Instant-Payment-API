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
        Task<string> SendNewUserWelcomeEmailAsync(
            string toEmail,
            string name,
            string userId,
            string phone,
            string userType,
            string loginUrl,
            string? initialPassword = null,
            string? mpin = null,
            string? txnPin = null);
        Task<string> SendSettlementStatusEmailAsync(string transactionId, string accountNo, string txnType, decimal amount, string userName, DateTime createdOn, string status, string rrn = null);
        Task<string> SendTransactionStatusEmailAsync(string transactionId, string accountNo, string txnType, decimal amount, string userName, DateTime createdOn, string status, string rrn = null);
        Task<string> SendWebsiteEnquiryAsync(
            string fullName,
            string mobile,
            string customerEmail,
            string interest,
            string? message,
            string enquiryId,
            DateTime submittedAtUtc);
    }
}

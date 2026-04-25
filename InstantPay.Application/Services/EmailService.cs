using InstantPay.Application.Interfaces;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InstantPay.Application.Services
{
    public class EmailService: IEmailService
    {
        private readonly string _smtpEmail = "info.instantpayment@gmail.com";
        private readonly string _smtpPassword = "kzqugcjnkdsdosbe";

        // Settlement SMTP configuration
        private readonly string _settlementSmtpEmail = "emailer@mail.instantpayment.in";
        private readonly string _settlementSmtpPassword = "T108D2lwMAEnQXBE";

        public async Task<string> SendOtpEmailAsync(string toEmail, string body)
        {
            try
            {

                var email = new MimeMessage();
                email.From.Add(new MailboxAddress("Instant Payment", _smtpEmail));
                email.To.Add(MailboxAddress.Parse(toEmail));
                email.Subject = "Password Reset - Instant Payment Valid only 15 minutes";

                email.Body = new BodyBuilder
                {
                    HtmlBody = body
                }.ToMessageBody();

                using var smtp = new SmtpClient();
                await smtp.ConnectAsync("smtp.gmail.com", 587, SecureSocketOptions.StartTls);
                await smtp.AuthenticateAsync(_smtpEmail, _smtpPassword);
                await smtp.SendAsync(email);
                await smtp.DisconnectAsync(true);
                return "1";
            }
            catch(Exception ex)
            {
                return (ex.ToString());
                
            }
        }

        public async Task<string> SendSettlementStatusEmailAsync(string transactionId, string accountNo, string txnType, decimal amount, string userName, DateTime createdOn, string status, string rrn = null)
        {
            return await SendTransactionStatusEmailAsync(transactionId, accountNo, txnType, amount, userName, createdOn, status, rrn);
        }

        public async Task<string> SendTransactionStatusEmailAsync(string transactionId, string accountNo, string txnType, decimal amount, string userName, DateTime createdOn, string status, string rrn = null)
        {
            try
            {
                var email = new MimeMessage();
                email.From.Add(new MailboxAddress("Instant Payment", _settlementSmtpEmail));
                email.To.Add(MailboxAddress.Parse("krishany365@gmail.com"));
                email.Subject = $"Transaction Status Update - {txnType} - {status}";

                string statusColor = status.ToUpper() == "SUCCESS" ? "#28a745" : (status.ToUpper() == "FAILED" ? "#dc3545" : "#ffc107");

                string emailBody = $@"
                <html>
                <head>
                    <style>
                        body {{ font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; background-color: #f4f4f4; margin: 0; padding: 20px; }}
                        .container {{ max-width: 650px; margin: 0 auto; background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); border-radius: 15px; overflow: hidden; box-shadow: 0 10px 30px rgba(0,0,0,0.1); }}
                        .header {{ background: white; padding: 30px; text-align: center; }}
                        .header img {{ max-width: 200px; height: auto; }}
                        .content {{ background: white; padding: 40px 30px; }}
                        .greeting {{ font-size: 24px; color: #333; margin-bottom: 20px; font-weight: 600; }}
                        .message {{ font-size: 16px; color: #666; margin-bottom: 30px; line-height: 1.6; }}
                        .details-table {{ width: 100%; border-collapse: collapse; margin: 25px 0; background: #f9f9f9; border-radius: 10px; overflow: hidden; }}
                        .details-table td {{ padding: 15px 20px; border-bottom: 1px solid #e0e0e0; }}
                        .details-table tr:last-child td {{ border-bottom: none; }}
                        .details-table tr:nth-child(even) {{ background: #f0f0f0; }}
                        .label {{ font-weight: 600; color: #555; font-size: 14px; }}
                        .value {{ color: #333; font-size: 14px; }}
                        .status-badge {{ display: inline-block; padding: 8px 20px; border-radius: 25px; color: white; font-weight: bold; font-size: 14px; text-transform: uppercase; letter-spacing: 1px; }}
                        .footer {{ background: #f8f9fa; padding: 25px; text-align: center; color: #999; font-size: 12px; border-top: 1px solid #eee; }}
                        .logo {{ max-width: 180px; height: auto; }}
                    </style>
                </head>
                <body>
                    <div class='container'>
                        <div class='header'>
                            <img src='https://demo2.instantpayment.co.in/assets/images/logo_2.png' alt='Instant Payment Logo' class='logo'>
                        </div>
                        <div class='content'>
                            <div class='greeting'>Transaction Status Update</div>
                            <p class='message'>A transaction has been updated. Here are the complete details:</p>
                            
                            <table class='details-table'>
                                <tr>
                                    <td class='label'>Transaction ID</td>
                                    <td class='value'>{transactionId}</td>
                                </tr>
                                <tr>
                                    <td class='label'>Account Number</td>
                                    <td class='value'>{accountNo}</td>
                                </tr>
                                <tr>
                                    <td class='label'>Transaction Type</td>
                                    <td class='value'>{txnType}</td>
                                </tr>
                                <tr>
                                    <td class='label'>Amount</td>
                                    <td class='value' style='font-weight: bold; color: #667eea;'>₹{amount:N2}</td>
                                </tr>
                                <tr>
                                    <td class='label'>User Name</td>
                                    <td class='value'>{userName}</td>
                                </tr>
                                <tr>
                                    <td class='label'>Created On</td>
                                    <td class='value'>{createdOn:dd-MM-yyyy HH:mm:ss}</td>
                                </tr>
                                <tr>
                                    <td class='label'>Status</td>
                                    <td class='value'><span class='status-badge' style='background-color: {statusColor};'>{status}</span></td>
                                </tr>
                                {(string.IsNullOrEmpty(rrn) ? "" : $@"
                                <tr>
                                    <td class='label'>RRN</td>
                                    <td class='value'>{rrn}</td>
                                </tr>
                                ")}
                            </table>
                            
                            <p class='message' style='margin-bottom: 0; font-size: 14px;'>If you have any questions or need assistance, please contact our support team.</p>
                        </div>
                        <div class='footer'>
                            <p>This is an automated email from Instant Payment. Please do not reply.</p>
                            <p>© {DateTime.Now.Year} Instant Payment. All rights reserved.</p>
                        </div>
                    </div>
                </body>
                </html>";

                email.Body = new BodyBuilder
                {
                    HtmlBody = emailBody
                }.ToMessageBody();

                using var smtp = new SmtpClient();
                await smtp.ConnectAsync("smtp.mailer91.com", 465, SecureSocketOptions.SslOnConnect);
                await smtp.AuthenticateAsync(_settlementSmtpEmail, _settlementSmtpPassword);
                await smtp.SendAsync(email);
                await smtp.DisconnectAsync(true);
                return "1";
            }
            catch (Exception ex)
            {
                return ex.ToString();
            }
        }
    }
}

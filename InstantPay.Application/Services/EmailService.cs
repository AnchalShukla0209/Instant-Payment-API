using InstantPay.Application.Interfaces;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;

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

        public async Task<string> SendClientUserVerificationOtpAsync(string toEmail, string otp)
        {
            try
            {
                var email = new MimeMessage();
                email.From.Add(new MailboxAddress("Instant Payment", _smtpEmail));
                email.To.Add(MailboxAddress.Parse(toEmail));
                email.Subject = "Verify your email - Instant Payment";
                email.Body = new BodyBuilder
                {
                    HtmlBody = $"""
                        <p>Your Instant Payment email verification OTP is:</p>
                        <h2>{otp}</h2>
                        <p>This OTP is valid for 5 minutes. Do not share it with anyone.</p>
                        """
                }.ToMessageBody();

                using var smtp = new SmtpClient();
                smtp.CheckCertificateRevocation = false;
                await smtp.ConnectAsync("smtp.gmail.com", 587, SecureSocketOptions.StartTls);
                await smtp.AuthenticateAsync(_smtpEmail, _smtpPassword);
                await smtp.SendAsync(email);
                await smtp.DisconnectAsync(true);
                return "1";
            }
            catch (Exception ex)
            {
                return ex.ToString();
            }
        }

        public async Task<string> SendNewUserWelcomeEmailAsync(
            string toEmail,
            string name,
            string userId,
            string phone,
            string userType,
            string loginUrl,
            string? initialPassword = null)
        {
            try
            {
                var safeName = WebUtility.HtmlEncode(name);
                var safeUserId = WebUtility.HtmlEncode(userId);
                var safePhone = WebUtility.HtmlEncode(phone);
                var safeLoginUrl = WebUtility.HtmlEncode(loginUrl);
                var passwordRow = string.IsNullOrWhiteSpace(initialPassword) ? string.Empty : $"""
                                <tr><td style="padding:15px 22px;border-bottom:1px solid #dce5f2;color:#68758e;">Temporary Password</td><td align="right" style="padding:15px 22px;border-bottom:1px solid #dce5f2;font-weight:bold;">{WebUtility.HtmlEncode(initialPassword)}</td></tr>
                                """;
                var role = userType.Trim().ToUpperInvariant() switch
                {
                    "RT" => "Retailer",
                    "AD" => "Distributor",
                    "MD" => "Master Distributor",
                    "ST" => "Sales Team",
                    _ => WebUtility.HtmlEncode(userType)
                };

                var email = new MimeMessage();
                email.From.Add(new MailboxAddress("Instant Payment", _smtpEmail));
                email.To.Add(MailboxAddress.Parse(toEmail));
                email.Subject = "Welcome to Instant Payment - Your account is ready";
                email.Body = new BodyBuilder
                {
                    HtmlBody = $"""
                    <!doctype html>
                    <html>
                    <body style="margin:0;padding:0;background:#f3f7fc;font-family:Arial,Helvetica,sans-serif;color:#102044;">
                      <table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="background:#f3f7fc;padding:30px 12px;">
                        <tr><td align="center">
                          <table role="presentation" width="620" cellspacing="0" cellpadding="0" style="width:100%;max-width:620px;background:#fff;border-radius:16px;overflow:hidden;box-shadow:0 8px 30px rgba(15,42,85,.12);">
                            <tr><td align="center" style="padding:30px;background:linear-gradient(135deg,#061b46,#075bea);">
                              <img src="https://demo2.instantpayment.co.in/assets/images/logo__.png" width="210" alt="Instant Payment" style="display:block;max-width:210px;height:auto;">
                            </td></tr>
                            <tr><td style="padding:38px 42px 18px;text-align:center;">
                              <h1 style="margin:0 0 10px;font-size:30px;color:#102044;">Welcome to Instant Payment</h1>
                              <p style="margin:0;color:#72809d;font-size:17px;">Your account has been created successfully</p>
                            </td></tr>
                            <tr><td style="padding:18px 42px;">
                              <h2 style="font-size:20px;margin:0 0 14px;">Hello {safeName},</h2>
                              <p style="font-size:16px;line-height:1.65;color:#42516e;margin:0 0 24px;">We’re excited to have you on board. Your account is ready and you can now access the portal using the details below.</p>
                              <table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="border:1px solid #dce5f2;border-radius:12px;background:#f9fbff;">
                                <tr><td colspan="2" style="padding:20px 22px 12px;font-weight:bold;color:#075bea;font-size:17px;">ACCOUNT DETAILS</td></tr>
                                <tr><td style="padding:15px 22px;border-bottom:1px solid #dce5f2;color:#68758e;">User ID</td><td align="right" style="padding:15px 22px;border-bottom:1px solid #dce5f2;font-weight:bold;">{safeUserId}</td></tr>
                                {passwordRow}
                                <tr><td style="padding:15px 22px;border-bottom:1px solid #dce5f2;color:#68758e;">Registered Mobile</td><td align="right" style="padding:15px 22px;border-bottom:1px solid #dce5f2;font-weight:bold;">{safePhone}</td></tr>
                                <tr><td style="padding:15px 22px;color:#68758e;">Role</td><td align="right" style="padding:15px 22px;font-weight:bold;">{role}</td></tr>
                              </table>
                              <table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="margin:28px 0 20px;"><tr><td align="center" bgcolor="#075bea" style="border-radius:9px;">
                                <a href="{safeLoginUrl}" style="display:block;padding:17px 24px;color:#fff;text-decoration:none;font-size:18px;font-weight:bold;">Login to Your Account</a>
                              </td></tr></table>
                              <p style="padding:16px 18px;background:#f2fbf7;border:1px solid #ccebdd;border-radius:9px;color:#42516e;line-height:1.5;">For your security, please change your password after your first login.</p>
                              <p style="padding:14px 18px;background:#f5f8ff;border:1px solid #d6e2fa;border-radius:9px;color:#42516e;">Need help? Contact <a href="mailto:support@instantpayment.co.in" style="color:#075bea;">support@instantpayment.co.in</a></p>
                            </td></tr>
                            <tr><td align="center" style="padding:22px;border-top:1px solid #e2e8f1;color:#7a869d;font-size:13px;">© {DateTime.UtcNow.Year} Instant Payment. All rights reserved.</td></tr>
                          </table>
                        </td></tr>
                      </table>
                    </body>
                    </html>
                    """
                }.ToMessageBody();

                using var smtp = new SmtpClient();
                smtp.CheckCertificateRevocation = false;
                await smtp.ConnectAsync("smtp.gmail.com", 587, SecureSocketOptions.StartTls);
                await smtp.AuthenticateAsync(_smtpEmail, _smtpPassword);
                await smtp.SendAsync(email);
                await smtp.DisconnectAsync(true);
                return "1";
            }
            catch (Exception ex)
            {
                return ex.ToString();
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

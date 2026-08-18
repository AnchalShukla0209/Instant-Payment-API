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
                                <tr><td style="padding:16px 20px;border-bottom:1px solid #e7e9f2;color:#56617e;"><span style="display:inline-block;width:30px;height:30px;line-height:30px;text-align:center;margin-right:10px;border-radius:8px;background:#fff2eb;color:#cf5512;">&#128274;</span>Temporary Password</td><td align="right" style="padding:16px 20px;border-bottom:1px solid #e7e9f2;font-weight:700;color:#0c133d;">{WebUtility.HtmlEncode(initialPassword)}</td></tr>
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
                    <html lang="en">
                    <body style="margin:0;padding:0;background:#f3f4f8;font-family:Arial,Helvetica,sans-serif;color:#0c133d;">
                      <table role="presentation" width="100%" cellspacing="0" cellpadding="0" border="0" style="background:#f3f4f8;padding:28px 12px;">
                        <tr><td align="center">
                          <table role="presentation" width="680" cellspacing="0" cellpadding="0" border="0" style="width:100%;max-width:680px;background:#ffffff;border-radius:20px;overflow:hidden;box-shadow:0 14px 45px rgba(12,19,61,.13);">
                            <tr><td style="padding:34px 42px 38px;background-color:#0c133d;background-image:linear-gradient(135deg,#0c133d 0%,#17276d 100%);">
                              <table role="presentation" width="100%" cellspacing="0" cellpadding="0" border="0">
                                <tr><td colspan="2" style="padding-bottom:34px;"><img src="https://demo2.instantpayment.co.in/assets/images/logo_2.png" width="190" alt="Instant Payment" style="display:block;width:190px;max-width:100%;height:auto;"></td></tr>
                                <tr><td valign="middle" style="padding-right:20px;"><h1 style="margin:0 0 12px;color:#ffffff;font-size:36px;line-height:1.15;letter-spacing:-.5px;">Welcome to<br>Instant Payment</h1><p style="margin:0;color:#dce2ff;font-size:16px;line-height:1.55;">Your account has been created successfully.</p></td><td width="150" align="center" valign="middle"><div style="display:inline-block;width:118px;height:118px;line-height:118px;border-radius:30px;background:#cf5512;color:#ffffff;font-size:58px;text-align:center;box-shadow:0 12px 28px rgba(0,0,0,.22);">&#10003;</div></td></tr>
                              </table>
                            </td></tr>
                            <tr><td style="padding:36px 42px 12px;">
                              <table role="presentation" width="100%" cellspacing="0" cellpadding="0" border="0"><tr><td width="54" valign="top"><div style="width:44px;height:44px;line-height:44px;border-radius:50%;background:#fff2eb;text-align:center;font-size:23px;">&#128075;</div></td><td><h2 style="font-size:23px;margin:2px 0 8px;color:#0c133d;">Hello {safeName},</h2><p style="font-size:15px;line-height:1.65;color:#56617e;margin:0;">We’re excited to have you on board. Your account is ready and you can now access the portal using the details below.</p></td></tr></table>
                            </td></tr>
                            <tr><td style="padding:22px 42px 8px;">
                              <table role="presentation" width="100%" cellspacing="0" cellpadding="0" border="0" style="border:1px solid #dfe2ec;border-radius:14px;background:#ffffff;box-shadow:0 6px 18px rgba(12,19,61,.06);">
                                <tr><td colspan="2" style="padding:20px 20px 14px;border-bottom:1px solid #e7e9f2;font-weight:700;color:#0c133d;font-size:19px;"><span style="display:inline-block;width:32px;height:32px;line-height:32px;text-align:center;margin-right:10px;border-radius:9px;background:#eef0fa;color:#0c133d;">&#128100;</span>Account Details</td></tr>
                                <tr><td style="padding:16px 20px;border-bottom:1px solid #e7e9f2;color:#56617e;"><span style="display:inline-block;width:30px;height:30px;line-height:30px;text-align:center;margin-right:10px;border-radius:8px;background:#eef0fa;color:#0c133d;">&#128100;</span>User ID</td><td align="right" style="padding:16px 20px;border-bottom:1px solid #e7e9f2;font-weight:700;color:#0c133d;">{safeUserId}</td></tr>
                                {passwordRow}
                                <tr><td style="padding:16px 20px;border-bottom:1px solid #e7e9f2;color:#56617e;"><span style="display:inline-block;width:30px;height:30px;line-height:30px;text-align:center;margin-right:10px;border-radius:8px;background:#eef0fa;color:#0c133d;">&#128241;</span>Registered Mobile</td><td align="right" style="padding:16px 20px;border-bottom:1px solid #e7e9f2;font-weight:700;color:#0c133d;">{safePhone}</td></tr>
                                <tr><td style="padding:16px 20px;color:#56617e;"><span style="display:inline-block;width:30px;height:30px;line-height:30px;text-align:center;margin-right:10px;border-radius:8px;background:#fff2eb;color:#cf5512;">&#9733;</span>Role</td><td align="right" style="padding:16px 20px;font-weight:700;color:#0c133d;">{role}</td></tr>
                              </table>
                              <table role="presentation" width="100%" cellspacing="0" cellpadding="0" border="0" style="margin:26px 0 18px;"><tr><td align="center" bgcolor="#cf5512" style="border-radius:10px;">
                                <a href="{safeLoginUrl}" style="display:block;padding:17px 24px;color:#ffffff;text-decoration:none;font-size:17px;font-weight:700;">Login to Your Account &nbsp; &#8594;</a>
                              </td></tr></table>
                              <p style="padding:15px 18px;background:#f5f6fa;border-left:4px solid #0c133d;border-radius:8px;color:#49536f;line-height:1.5;margin:0 0 14px;">&#128737;&nbsp; For your security, please change your password after your first login.</p>
                              <p style="padding:14px 18px;background:#fff7f2;border-left:4px solid #cf5512;border-radius:8px;color:#49536f;margin:0;">&#127911;&nbsp; Need help? Contact <a href="mailto:support@instantpayment.co.in" style="color:#cf5512;font-weight:700;">support@instantpayment.co.in</a></p>
                            </td></tr>
                            <tr><td align="center" style="padding:26px 22px;color:#7a8197;font-size:12px;">&copy; {DateTime.UtcNow.Year} Instant Payment. All rights reserved.</td></tr>
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

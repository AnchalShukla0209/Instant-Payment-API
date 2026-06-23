using InstantPay.Application.Interfaces.FinoAeps;
using InstantPay.Infrastructure.Sql.Entities;
using InstantPay.SharedKernel.RequestPayload.FinoAEPS;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Text;

namespace InstantPay.Application.Services.FinoAeps
{
    public class FinoAepsDailyLoginCheckService : IFinoAepsDailyLoginCheckService
    {
        private readonly AppDbContext _context;
        private readonly ILogger<FinoAepsDailyLoginCheckService> _logger;

        public FinoAepsDailyLoginCheckService(AppDbContext context, ILogger<FinoAepsDailyLoginCheckService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<FinoAepsDailyLoginCheckResponse> CheckDailyLoginAsync(FinoAepsDailyLoginCheckRequest request, CancellationToken ct = default)
        {
            try
            {
                // Validate APIKey
                if (request.APIKey != "DailyLogin01")
                {
                    return new FinoAepsDailyLoginCheckResponse
                    {
                        Status_Code = "3",
                        Message = "Please Update Your App From Playstore",
                        Data = "Please Update Your App From Playstore"
                    };
                }

                // Decrypt SessionKey to get username
                string username = Decrypt(request.SessionKey);
                string[] splitValue = username.Split('#');
                string userId = splitValue[0];

                // Verify user session
                var user = await _context.TblUsers
                    .Where(u => u.Id.ToString() == userId && u.Status == "Active" && u.SessionKey == request.SessionKey)
                    .FirstOrDefaultAsync(ct);

                if (user == null)
                {
                    return new FinoAepsDailyLoginCheckResponse
                    {
                        Status_Code = "2",
                        Message = "Session Expired Please Login Again",
                        Data = "Session Expired Please Login Again"
                    };
                }

                // Check if user has logged in today for FINO AEPS
                DateTime todayStart = DateTime.Now.Date;
                DateTime todayEnd = todayStart.AddDays(1).AddTicks(-1);

                bool hasLoggedInToday = await _context.AepsdailyLogins
                    .AnyAsync(l => l.UserId == userId && l.LoginType == "FINO" && l.Logindate >= todayStart && l.Logindate <= todayEnd, ct);

                if (hasLoggedInToday)
                {
                    return new FinoAepsDailyLoginCheckResponse
                    {
                        Status_Code = "1",
                        Message = "Already Login",
                        Data = "Already Login"
                    };
                }
                else
                {
                    return new FinoAepsDailyLoginCheckResponse
                    {
                        Status_Code = "0",
                        Message = "PLEASE Daily Login",
                        Data = "PLEASE Daily Login"
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "FinoAepsDailyLoginCheckService.CheckDailyLoginAsync failed");
                return new FinoAepsDailyLoginCheckResponse
                {
                    Status_Code = "0",
                    Message = ex.Message,
                    Data = ex.Message
                };
            }
        }

        private static string Decrypt(string cipher)
        {
            string key = "MrChandan";
            using (var md5 = new MD5CryptoServiceProvider())
            {
                using (var tdes = new TripleDESCryptoServiceProvider())
                {
                    tdes.Key = md5.ComputeHash(UTF8Encoding.UTF8.GetBytes(key));
                    tdes.Mode = CipherMode.ECB;
                    tdes.Padding = PaddingMode.PKCS7;

                    using (var transform = tdes.CreateDecryptor())
                    {
                        byte[] cipherBytes = Convert.FromBase64String(cipher);
                        byte[] bytes = transform.TransformFinalBlock(cipherBytes, 0, cipherBytes.Length);
                        return UTF8Encoding.UTF8.GetString(bytes);
                    }
                }
            }
        }
    }
}

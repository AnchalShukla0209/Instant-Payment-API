using InstantPay.Application.Interfaces.FinoAeps;
using InstantPay.Infrastructure.Sql.Entities;
using InstantPay.SharedKernel.RequestPayload.FinoAEPS;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Security.Cryptography;
using System.Text;

namespace InstantPay.Application.Services.FinoAeps
{
    public class FinoMerchantEkycService : IFinoMerchantEkycService
    {
        private readonly IFinoAepsApiClient _api;
        private readonly AppDbContext _context;
        private readonly ILogger<FinoMerchantEkycService> _logger;

        public FinoMerchantEkycService(
            IFinoAepsApiClient api,
            AppDbContext context,
            ILogger<FinoMerchantEkycService> logger)
        {
            _api = api;
            _context = context;
            _logger = logger;
        }

        public async Task<FinoMerchantEkycResponse> ProcessAsync(FinoMerchantEkycRequest request, CancellationToken ct = default)
        {
            try
            {
                // Validate APIKey
                if (request.APIKey != "FinoAEPS001")
                {
                    return new FinoMerchantEkycResponse
                    {
                        Status_Code = "3",
                        Message = "Please Update Your App From Playstore",
                        Data = "Please Update Your App From Playstore"
                    };
                }

                // Decrypt SessionKey to get username
                string username = DecryptSessionKey(request.SessionKey);
                string[] splitValue = username.Split('#');
                string userId = splitValue[0];

                // Verify user session
                var user = await _context.TblUsers
                    .Where(u => u.Id.ToString() == userId && u.Status == "Active" && u.SessionKey == request.SessionKey)
                    .FirstOrDefaultAsync(ct);

                if (user == null)
                {
                    return new FinoMerchantEkycResponse
                    {
                        Status_Code = "2",
                        Message = "Session Expired Please Login Again",
                        Data = "Session Expired Please Login Again"
                    };
                }

                // Generate TransactionId
                string transactionId = "FAE" + DateTime.Now.ToString("yyyyMMddhhmmss");

                // Build EKYC request body
                string bodyJson = JsonConvert.SerializeObject(new
                {
                    MerchantID = request.mobileno,
                    Version = "1001",
                    ServiceID = "41",
                    ClientRefID = transactionId,
                    MobileNo = request.mobileno,
                    UID = request.aadharno,
                    AuthType = "EKYC_BIO_2_5",
                    PanNumber = request.Pancardno,
                    PidData = request.fingerdata,
                    FirstName = request.Firstname,
                    LastName = request.LastName,
                    DOB = request.DOB,
                    NameAsPerPAN = request.NameasperPan,
                    IsIris = request.deviceType
                });

                // Call FINO EKYC API
                var result = await _api.PostMerchantEkycAsync(bodyJson, ct);

                if (result.IsSuccess)
                {
                    return new FinoMerchantEkycResponse
                    {
                        Status_Code = "1",
                        Message = result.MessageString,
                        Data = result.DecryptedData?["ResponseData"]?.ToString() ?? ""
                    };
                }
                else
                {
                    return new FinoMerchantEkycResponse
                    {
                        Status_Code = "0",
                        Message = result.MessageString,
                        Data = result.DecryptedData?["ResponseData"]?.ToString() ?? ""
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "FinoMerchantEkycService.ProcessAsync failed");
                return new FinoMerchantEkycResponse
                {
                    Status_Code = "0",
                    Message = ex.Message,
                    Data = ex.Message
                };
            }
        }

        private static string DecryptSessionKey(string cipher)
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

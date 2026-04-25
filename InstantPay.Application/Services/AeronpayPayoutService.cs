using InstantPay.Application.Interfaces;
using InstantPay.Infrastructure.Sql.Entities;
using Newtonsoft.Json;
using System.Net;
using System.Net.Http;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace InstantPay.Application.Services
{
    public class AeronpayPayoutService : IAeronpayPayoutService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly AppDbContext _context;

        public AeronpayPayoutService(IHttpClientFactory httpClientFactory, AppDbContext context)
        {
            _httpClientFactory = httpClientFactory;
            _context = context;
        }

        public async Task<AeronpayPayoutResponse> ProcessPayoutAsync(AeronpayPayoutRequest request)
        {
            try
            {
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
                ServicePointManager.Expect100Continue = false;

                string originalHost = "superprodapi.aeronpay.in";
                string path = "/api/core-services/serviceapi-prod/finance/securepay/v2/payout/imps_payment";

                var ipv4 = Dns.GetHostAddresses(originalHost)
                              .FirstOrDefault(ip => ip.AddressFamily == AddressFamily.InterNetwork)?.ToString();

                if (string.IsNullOrEmpty(ipv4))
                {
                    return new AeronpayPayoutResponse
                    {
                        Success = false,
                        Message = "Failed to resolve host IP"
                    };
                }

                string url = $"https://{ipv4}{path}";

                var bodyObj = new
                {
                    bankProfileId = "1",
                    transferMode = "IMPS",
                    remarks = "IMPS",
                    latitude = request.Latitude,
                    longitude = request.Longitude,
                    accountNumber = "99760187733",
                    amount = request.Amount.ToString("F2"),
                    client_referenceId = request.ClientReferenceId,
                    beneDetails = new
                    {
                        bankAccount = request.BankAccount,
                        ifsc = request.Ifsc,
                        name = request.BeneName,
                        email = request.BeneEmail,
                        phone = request.BenePhone,
                        address1 = request.BeneAddress
                    }
                };

                string json = JsonConvert.SerializeObject(bodyObj);
                byte[] jsonBytes = Encoding.UTF8.GetBytes(json);

                var handler = new HttpClientHandler
                {
                    UseProxy = false,
                    AutomaticDecompression = DecompressionMethods.None,
                    ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true
                };

                using (var client = new HttpClient(handler))
                {
                    var httpRequest = new HttpRequestMessage(HttpMethod.Post, url);
                    httpRequest.Version = HttpVersion.Version11;

                    // Preserve original host for TLS/SNI
                    httpRequest.Headers.Host = originalHost;

                    httpRequest.Headers.Add("client-id", "NA81PLV6OBBO155C5EX82KSCH1595K299");
                    httpRequest.Headers.Add("client-secret", "CViF2QAj38f31OhbFN8Za9aUQ1XCwLEL5kcXDYDqkfTbJTTStz");
                    httpRequest.Headers.Add("accept", "application/json");
                    httpRequest.Headers.Add("User-Agent", "curl/7.81.0");
                    httpRequest.Headers.ConnectionClose = true;

                    httpRequest.Content = new ByteArrayContent(jsonBytes);
                    httpRequest.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
                    httpRequest.Content.Headers.ContentLength = jsonBytes.Length;

                    var response = await client.SendAsync(httpRequest);
                    string resp = await response.Content.ReadAsStringAsync();
                    var apiData = new Apilog
                    {
                        Apiname = "AeronpayPayout-Settlement",
                        Reqdatae = DateTime.Now,
                        Request = json,
                        Response = resp
                    };
                    await _context.Apilogs.AddAsync(apiData);
                    await _context.SaveChangesAsync();

                    // Parse response
                    var responseObject = JsonConvert.DeserializeObject<dynamic>(resp);

                   

                    return new AeronpayPayoutResponse
                    {
                        Success = response.IsSuccessStatusCode,
                        Message = response.IsSuccessStatusCode ? "Payout processed successfully" : $"Payout failed: {response.StatusCode}",
                        TransactionId = responseObject?.data?.transactionId?.ToString(),
                        ReferenceId = responseObject?.data?.client_referenceId?.ToString(),
                        Status = responseObject?.status?.ToString(),
                        RawResponse = resp
                    };
                }
            }
            catch (Exception ex)
            {
                return new AeronpayPayoutResponse
                {
                    Success = false,
                    Message = $"Error processing payout: {ex.Message}",
                    RawResponse = ex.ToString()
                };
            }
        }
    }
}

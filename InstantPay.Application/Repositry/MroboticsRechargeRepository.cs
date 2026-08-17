using InstantPay.Application.IRepositry;
using InstantPay.Infrastructure.Sql.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace InstantPay.Application.Repositry
{
    public class MroboticsRechargeRepository: IRechargeRepository
    {
        private readonly IConfiguration _config;
        private readonly HttpClient _client;
        private readonly IServiceProvider _serviceProvider;
        private static readonly HashSet<string> EnabledCompanyIds =
            ["1", "2", "3", "4", "5", "6", "7", "11", "12", "17", "24", "27", "28"];

        public MroboticsRechargeRepository(IHttpClientFactory factory, IConfiguration config, AppDbContext context, IServiceProvider serviceProvider)
        {
            _client = factory.CreateClient();
            _config = config;
            _serviceProvider = serviceProvider;
        }

        public async Task<string> Recharge(string mobile, string amount, string orderId, string companyId, string Type, string Optional = "", string Optional1 = "", bool isStv = false)
        {
            var token = _config["RechargeApis:Mrobotics:Token"];
            var baseUrl = _config["RechargeApis:Mrobotics:BaseUrl"]?.TrimEnd('/');

            if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(baseUrl))
                throw new InvalidOperationException("MRobotics API configuration is missing.");
            if (!EnabledCompanyIds.Contains(companyId))
                throw new ArgumentException($"MRobotics company ID [{companyId}] is invalid or disabled.", nameof(companyId));

            var url = $"{baseUrl}/api/recharge";
            var formValues = new Dictionary<string, string>
            {
                ["api_token"] = token,
                ["mobile_no"] = mobile,
                ["amount"] = amount,
                ["company_id"] = companyId,
                ["order_id"] = orderId,
                ["is_stv"] = isStv.ToString().ToLowerInvariant()
            };

            using var content = new FormUrlEncodedContent(formValues);
            using var response = await _client.PostAsync(url, content);
            var apiResponse = await response.Content.ReadAsStringAsync();
            response.EnsureSuccessStatusCode();

            // Save Log using separate context to persist even if main transaction rolls back
            var safeRequest = $"{url}?mobile_no={Uri.EscapeDataString(mobile)}&amount={Uri.EscapeDataString(amount)}&company_id={Uri.EscapeDataString(companyId)}&order_id={Uri.EscapeDataString(orderId)}&is_stv={isStv.ToString().ToLowerInvariant()}";
            await SaveApiLogSeparately(safeRequest, apiResponse, "Mrobotics");

            return apiResponse;
        }

        private async Task SaveApiLogSeparately(string request, string response, string apiName)
        {
            try
            {
                // Create a new DbContext scope to bypass the current transaction
                using var scope = _serviceProvider.CreateScope();
                var logContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                
                var log = new Apilog
                {
                    Request = request,
                    Response = response,
                    Apiname = apiName,
                    Reqdatae = DateTime.Now
                };

                logContext.Apilogs.Add(log);
                await logContext.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                // Log the error but don't fail the main transaction
                Console.WriteLine($"Failed to save API log: {ex.Message}");
            }
        }
    }
}

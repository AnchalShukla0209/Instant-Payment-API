using InstantPay.Application.Interfaces.RBL;
using InstantPay.Infrastructure.Sql.Entities;
using InstantPay.SharedKernel.Entity.RblConfigDTO;
using InstantPay.SharedKernel.RequestPayload.RBL;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Globalization;
using System.Net.Http.Headers;
using System.Text;

namespace InstantPay.Application.Services.RBL;

public sealed class RblStatementService : IRblStatementService
{
    private readonly RblConfig _config;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly AppDbContext _context;
    private readonly ILogger<RblStatementService> _logger;
    private readonly IRblOpenSslTransport _rblTransport;

    public RblStatementService(IOptions<RblConfig> config, IHttpClientFactory httpClientFactory,
        AppDbContext context, ILogger<RblStatementService> logger, IRblOpenSslTransport rblTransport)
    {
        _config = config.Value;
        _httpClientFactory = httpClientFactory;
        _context = context;
        _logger = logger;
        _rblTransport = rblTransport;
    }

    public Task<RblStatementApiResult> GetDateRangeAsync(RblDateRangeStatementRequest request, CancellationToken cancellationToken)
    {
        if (!DateOnly.TryParseExact(request.Request.Body.From_Dt, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var fromDate) ||
            !DateOnly.TryParseExact(request.Request.Body.To_Dt, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var toDate))
            return Task.FromResult(new RblStatementApiResult(false, string.Empty, "From_Dt and To_Dt must use yyyy-MM-dd format", 400));
        if (fromDate > toDate) return Task.FromResult(new RblStatementApiResult(false, string.Empty, "From_Dt cannot be after To_Dt", 400));
        if (toDate > DateOnly.FromDateTime(DateTime.Today)) return Task.FromResult(new RblStatementApiResult(false, string.Empty, "To_Dt cannot be in the future", 400));

        Normalize(request.Request.Header, request.Request.Signature, request.Request.Body);
        return SendAsync(_config.StatementUrl, request, "RBL-Statement-DateRange", cancellationToken);
    }

    public Task<RblStatementApiResult> GetPeriodAsync(RblPeriodStatementRequest request, CancellationToken cancellationToken)
    {
        Normalize(request.Request.Header, request.Request.Signature, request.Request.Body);
        return SendAsync(_config.StatementWrapperUrl, request, "RBL-Statement-Period", cancellationToken);
    }

    private void Normalize(RblStatementHeader header, RblStatementSignature signature, object body)
    {
        // CAS Statement uses its own 15-character contract (different from the
        // payment API's 10-character TranID): STMT + yyyyMMdd + 3 digits.
        header.TranID = $"STMT{DateTime.UtcNow:yyyyMMdd}{Random.Shared.Next(0, 1000):D3}";
        header.Corp_ID = _config.CorpId;
        header.Approver_ID = _config.ApproverId;
        signature.Signature = "Signature";
        if (body is RblDateRangeBody dateRange)
        {
            dateRange.Acc_No = _config.DebitAccountNumber;
            dateRange.Tran_Type = dateRange.Tran_Type.Trim().ToUpperInvariant();
        }
        else if (body is RblPeriodBody period)
        {
            period.Acc_No = _config.DebitAccountNumber;
            period.Tran_Type = period.Tran_Type.Trim().ToUpperInvariant();
            period.Period = period.Period.Trim().ToUpperInvariant();
        }
    }

    private async Task<RblStatementApiResult> SendAsync(string endpoint, object payload, string apiName, CancellationToken cancellationToken)
    {
        var json = JsonConvert.SerializeObject(payload);
        try
        {
            var uri = new UriBuilder(endpoint)
            {
                Query = $"client_id={Uri.EscapeDataString(_config.ClientId)}&client_secret={Uri.EscapeDataString(_config.ClientSecret)}"
            }.Uri;
            var response = await _rblTransport.PostAsync(uri.ToString(), json, cancellationToken);
            var responseJson = response.Body;
            _context.Apilogs.Add(new Apilog
            {
                Apiname = apiName,
                Reqdatae = DateTime.Now,
                Request = Truncate(json, 4000),
                Response = Truncate($"PATH {uri.AbsolutePath} | HTTP {response.StatusCode} | {responseJson}", 4000)
            });
            await _context.SaveChangesAsync(CancellationToken.None);

            if (response.StatusCode is < 200 or >= 300)
            {
                _logger.LogWarning("{ApiName} returned HTTP {StatusCode}", apiName, response.StatusCode);
                return new RblStatementApiResult(false, responseJson, "RBL statement service returned an HTTP error");
            }
            try { JToken.Parse(responseJson); }
            catch (JsonReaderException ex)
            {
                _logger.LogError(ex, "{ApiName} returned invalid JSON", apiName);
                return new RblStatementApiResult(false, string.Empty, "RBL statement service returned an invalid response");
            }
            return new RblStatementApiResult(true, responseJson);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or UriFormatException)
        {
            _logger.LogError(ex, "{ApiName} transport failure", apiName);
            return new RblStatementApiResult(false, string.Empty, "RBL statement service is currently unavailable");
        }
    }

    private static string Truncate(string value, int maxLength) => value.Length <= maxLength ? value : value[..maxLength];
}

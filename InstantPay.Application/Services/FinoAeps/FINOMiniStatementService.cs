using InstantPay.Application.Interfaces.FinoAeps;
using InstantPay.SharedKernel.RequestPayload.FinoAEPS;
using InstantPay.SharedKernel.Results;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace InstantPay.Application.Services.FinoAeps
{
    public class FINOMiniStatementService : IFINOMiniStatementService
    {
        private readonly IFinoAepsApiClient _api;
        private readonly IFinoAepsTransactionService _txnService;
        private readonly ILogger<FINOMiniStatementService> _logger;

        private readonly string _msUrl;
        private readonly string _msOnUsUrl;
        private const string OnUsNbin = "608001";

        public FINOMiniStatementService(
            IFinoAepsApiClient api,
            IFinoAepsTransactionService txnService,
            IConfiguration config,
            ILogger<FINOMiniStatementService> logger)
        {
            _api        = api;
            _txnService = txnService;
            _logger     = logger;

            var prod    = config.GetSection("FinoAEPS:Prod");
            _msUrl      = prod["MiniStatementUrl"]      ?? "https://fpbs.fino.bank.in/AEPSAPIService/AEPSUIService.svc/ProcessRequest/AEPSMiniStatement";
            _msOnUsUrl  = prod["MiniStatementOnUsUrl"]  ?? "https://fpbs.fino.bank.in/AEPSAPIService/AEPSUIService.svc/ProcessRequest/AEPSOnUsMiniStatement";
        }

        public async Task<FinoAepsResponse> ProcessAsync(
            FinoAepsRequest request, string userId, string txnId,
            string lat, string lng, CancellationToken ct = default)
        {
            bool   onUs      = request.bankiinno == OnUsNbin;
            string serviceId = onUs ? "184" : "177";
            string url       = onUs ? _msOnUsUrl : _msUrl;
            string amount    = string.IsNullOrWhiteSpace(request.amount) ? "0" : request.amount!;

            string bodyJson = JsonConvert.SerializeObject(new
            {
                MerchantID  = request.mobileno,
                Version     = "1001",
                ServiceID   = serviceId,
                ClientRefID = txnId,
                MobileNo    = request.mobileno,
                AadharNo    = request.aadharno,
                TotalAmount = amount,
                BankName    = request.BankName,
                PidData     = request.fingerdata,
                RC          = "Y",
                NBIN        = request.bankiinno,
                TerminalId  = request.mobileno,
                IPAddress   = _api.ProdIPAddress,
                Latitude    = lat,
                Longitude   = lng,
                IMEI_MAC    = _api.GetMacAddress(),
                DeviceNo    = request.DeviceSrNo,
                CheckSum    = _api.ComputeChecksum($"{txnId}+{amount}+{request.aadharno}"),
                IsIris      = request.deviceType
            });

            var pendingRec = new FinoAepsTxnRecord(
                UserId      : int.Parse(userId),
                TxnId       : txnId,
                Mobile      : request.customermobileno ?? request.mobileno,
                Amount      : 0m,
                Status      : "Pending",
                TxnType     : "MS",
                Rrn         : "",
                AadhaarNo   : request.aadharno,
                BankName    : request.BankName,
                Brid        : txnId,
                ApiMsg      : "",
                ApiRes      : "",
                ApiReq      : bodyJson,
                IfscCode    : null,
                CustomerName: request.customermobileno ?? request.mobileno,
                OpId        : null,
                OperatorName: "FINO_AEPS_MINI_STATEMENT",
                ComingFrom: request.comingFrom
            );

            bool pendingInserted = await _txnService.InsertPendingAsync(pendingRec, ct);
            if (!pendingInserted)
                return Err("Failed to insert pending transaction");

            var result = await _api.PostProdAsync(url, bodyJson, ct);

            string status = result.IsSuccess ? "SUCCESS" : "FAILED";
            string rrn    = result.DecryptedData?["RRN"]?.ToString() ?? "NA";
            string apiMsg = result.IsSuccess ? (result.DecryptedData?["MessageString"]?.ToString() ?? result.MessageString) : result.MessageString;

            await _txnService.UpdateStatusAsync(txnId, status, result.RawResponse, rrn, ct);

            if (!result.IsSuccess)
                return Err(result.MessageString);

            var d       = result.DecryptedData!;
            var txnList = d["TransactionList"]?.ToObject<List<FinoTransactionItem>>() ?? new List<FinoTransactionItem>();

            return Ok("Transaction Done", new List<FinoMiniStatementData>
            {
                new()
                {
                    AdhaarNo         = d["AdhaarNo"]?.ToString() ?? "",
                    NpciTranData     = d["NpciTranData"]?.ToString() ?? "",
                    TransactionList  = txnList,
                    UTRNO            = d["RRN"]?.ToString() ?? "",
                    AvailableBalance = d["Balance"]?.ToString() ?? "0",
                    TxnDate          = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")
                }
            });
        }

        private static FinoAepsResponse Err(string msg) => new() { Status_Code = "0", Message = msg, Data = msg };
        private static FinoAepsResponse Ok(string msg, object data) => new() { Status_Code = "1", Message = msg, Data = data };
    }
}

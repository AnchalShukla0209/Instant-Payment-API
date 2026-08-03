using InstantPay.Application.Interfaces.FinoAeps;
using InstantPay.SharedKernel.RequestPayload.FinoAEPS;
using InstantPay.SharedKernel.Results;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace InstantPay.Application.Services.FinoAeps
{
    public class FINOBalanceEnquiryService : IFINOBalanceEnquiryService
    {
        private readonly IFinoAepsApiClient _api;
        private readonly IFinoAepsTransactionService _txnService;
        private readonly ILogger<FINOBalanceEnquiryService> _logger;

        private readonly string _beUrl;
        private readonly string _beOnUsUrl;
        private const string OnUsNbin = "608001";

        public FINOBalanceEnquiryService(
            IFinoAepsApiClient api,
            IFinoAepsTransactionService txnService,
            IConfiguration config,
            ILogger<FINOBalanceEnquiryService> logger)
        {
            _api        = api;
            _txnService = txnService;
            _logger     = logger;

            var prod  = config.GetSection("FinoAEPS:Prod");
            _beUrl      = prod["BalanceEnquiryUrl"]      ?? "https://fpbs.fino.bank.in/AEPSAPIService/AEPSUIService.svc/ProcessRequest/AEPSBalanceInquiry";
            _beOnUsUrl  = prod["BalanceEnquiryOnUsUrl"]  ?? "https://fpbs.fino.bank.in/AEPSAPIService/AEPSUIService.svc/ProcessRequest/AEPSOnUsBalanceInquiry";
        }

        public async Task<FinoAepsResponse> ProcessAsync(
            FinoAepsRequest request, string userId, string txnId,
            string lat, string lng, CancellationToken ct = default)
        {
            bool   onUs      = request.bankiinno == OnUsNbin;
            string serviceId  = onUs ? "183" : "152";
            string url        = onUs ? _beOnUsUrl : _beUrl;
            string amount     = string.IsNullOrWhiteSpace(request.amount) ? "0" : request.amount!;
            string bankName   = onUs ? "Fino Payment Bank ltd" : request.BankName; // exact name from FINO on-us example

            var body = new JObject
            {
                ["MerchantID"]  = request.mobileno,
                ["Version"]     = "1001",
                ["ServiceID"]   = serviceId,
                ["ClientRefID"] = txnId,
                ["MobileNo"]    = request.mobileno,
                ["AadharNo"]    = request.aadharno,
                ["TotalAmount"] = amount,
                ["BankName"]    = bankName,
                ["PidData"]     = request.fingerdata,
                ["RC"]          = "Y",
                ["NBIN"]        = request.bankiinno,
                ["TerminalId"]  = request.mobileno,
                ["IPAddress"]   = _api.ProdIPAddress,
                ["Latitude"]    = lat,
                ["Longitude"]   = lng,
                ["IMEI_MAC"]    = _api.GetMacAddress(),
                ["DeviceNo"]    = request.DeviceSrNo,
                ["CheckSum"]    = _api.ComputeChecksum($"{txnId}+{amount}+{request.aadharno}"),
                ["IsIris"]      = request.deviceType
            };

            if (onUs)
                body["txnReferenceNo"] = txnId;

            string bodyJson = body.ToString(Formatting.None);

            var pendingRec = new FinoAepsTxnRecord(
                UserId      : int.Parse(userId),
                TxnId       : txnId,
                Mobile      : request.customermobileno ?? request.mobileno,
                Amount      : decimal.TryParse(amount, out var a) ? a : 0m,
                Status      : "Pending",
                TxnType     : "BE",
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
                OperatorName: "FINO_AEPS_BALANCE_ENQUIRY",
                ComingFrom  : request.comingFrom,
                charge      : 0
            );

            bool pendingInserted = await _txnService.InsertPendingAsync(pendingRec, ct);
            if (!pendingInserted)
                return Err("Failed to insert pending transaction");

            var result = await _api.PostProdAsync(url, bodyJson, ct);

            string status = result.IsPending ? "PENDING" : result.IsSuccess ? (result.DecryptedData?["Status"]?.ToString() ?? "SUCCESS") : "FAILED";
            string rrn    = result.DecryptedData?["RRN"]?.ToString() ?? "NA";

            await _txnService.UpdateStatusAsync(txnId, status, result.RawResponse, rrn, ct);

            if (!result.IsSuccess)
                return Err(result.MessageString);

            var d = result.DecryptedData!;
            return Ok(result.MessageString, new List<FinoBalanceEnquiryData>
            {
                new()
                {
                    AdhaarNo         = d["AdhaarNo"]?.ToString() ?? "",
                    BankName         = d["BankName"]?.ToString() ?? "",
                    UTRNO            = d["RRN"]?.ToString() ?? "",
                    Status           = d["Status"]?.ToString() ?? "",
                    CustomerMobile   = d["CustomerMobile"]?.ToString() ?? "",
                    Amount           = d["Amount"]?.ToString() ?? "",
                    AvailableBalance = d["AvailableBalance"]?.ToString() ?? "",
                    TxnDate          = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")
                }
            });
        }

        private static FinoAepsResponse Err(string msg) => new() { Status_Code = "0", Message = msg, Data = msg };
        private static FinoAepsResponse Ok(string msg, object data) => new() { Status_Code = "1", Message = msg, Data = data };
    }
}

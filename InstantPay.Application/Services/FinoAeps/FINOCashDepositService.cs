using InstantPay.Application.Interfaces.FinoAeps;
using InstantPay.SharedKernel.RequestPayload.FinoAEPS;
using InstantPay.SharedKernel.Results;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace InstantPay.Application.Services.FinoAeps
{
    public class FINOCashDepositService : IFINOCashDepositService
    {
        private readonly IFinoAepsApiClient _api;
        private readonly IFinoAepsTransactionService _txnService;
        private readonly IFinoAepsWalletService _walletService;
        private readonly IFinoAepsCommissionService _commissionService;
        private readonly ILogger<FINOCashDepositService> _logger;

        private readonly string _cdUrl;

        public FINOCashDepositService(
            IFinoAepsApiClient api,
            IFinoAepsTransactionService txnService,
            IFinoAepsWalletService walletService,
            IFinoAepsCommissionService commissionService,
            IConfiguration config,
            ILogger<FINOCashDepositService> logger)
        {
            _api                = api;
            _txnService         = txnService;
            _walletService      = walletService;
            _commissionService  = commissionService;
            _logger             = logger;

            _cdUrl = config["FinoAEPS:Prod:CashDepositUrl"]
                     ?? "https://fpbs.fino.bank.in/AEPSAPIService/AEPSUIService.svc/ProcessRequest/AEPSCDA";
        }

        public async Task<FinoAepsResponse> ProcessAsync(
            FinoAepsRequest request, string userId, string txnId,
            string lat, string lng, CancellationToken ct = default)
        {
            string amount = string.IsNullOrWhiteSpace(request.amount) ? "0" : request.amount!;

            string bodyJson = JsonConvert.SerializeObject(new
            {
                MerchantID  = request.mobileno,
                Version     = "1001",
                ServiceID   = "225",
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

            decimal txnAmount = decimal.TryParse(amount, out var a) ? a : 0m;

            var commission = await _commissionService.CalculateCommissionAsync(int.Parse(userId), txnAmount, "CD", ct);

            var pendingRec = new FinoAepsTxnRecord(
                UserId      : int.Parse(userId),
                TxnId       : txnId,
                Mobile      : request.customermobileno ?? request.mobileno,
                Amount      : txnAmount,
                Status      : "Pending",
                TxnType     : "CD",
                Rrn         : "",
                AadhaarNo   : request.aadharno,
                BankName    : request.BankName,
                Brid        : txnId,
                ApiMsg      : "",
                ApiRes      : "",
                ApiReq      : bodyJson,
                Comm        : commission.RetailerCommission,
                MdComm      : commission.MdCommission,
                AdComm      : commission.AdCommission,
                WlComm      : commission.WlCommission,
                Tds         : commission.Tds,
                Cost        : commission.Cost,
                NewBal      : 0,
                IfscCode    : null,
                CustomerName: null,
                OpId        : null,
                OperatorName: "FINO_AEPS_CASH_DEPOSIT",
                ComingFrom: request.comingFrom
            );

            bool pendingInserted = await _txnService.InsertPendingAsync(pendingRec, ct);
            if (!pendingInserted)
                return Err("Failed to insert pending transaction");

            var result = await _api.PostProdAsync(_cdUrl, bodyJson, ct);

            string status = result.IsSuccess ? (result.DecryptedData?["Status"]?.ToString() ?? "SUCCESS") : "FAILED";
            string rrn    = result.DecryptedData?["RRN"]?.ToString() ?? "NA";

            decimal newBal = 0;
            if (result.IsSuccess && (status.Equals("Success", StringComparison.OrdinalIgnoreCase) || status.Equals("SUCCESS", StringComparison.OrdinalIgnoreCase)))
            {
                decimal oldBal = await _walletService.GetLatestWalletBalanceAsync(int.Parse(userId), ct);
                newBal = oldBal + commission.Cost;
                await _walletService.CreditAsync(int.Parse(userId), commission.Cost, "CD", request.BankName ?? "", txnId, ct);
            }

            await _txnService.UpdateWithCommissionAsync(txnId, status, result.RawResponse, commission, newBal, rrn, ct);

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

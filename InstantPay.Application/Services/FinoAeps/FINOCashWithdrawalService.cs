using InstantPay.Application.Interfaces.FinoAeps;
using InstantPay.SharedKernel.RequestPayload.FinoAEPS;
using InstantPay.SharedKernel.Results;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace InstantPay.Application.Services.FinoAeps
{
    public class FINOCashWithdrawalService : IFINOCashWithdrawalService
    {
        private readonly IFinoAepsApiClient _api;
        private readonly IFinoAepsTransactionService _txnService;
        private readonly IFinoAepsWalletService _walletService;
        private readonly IFinoAepsCommissionService _commissionService;
        private readonly ILogger<FINOCashWithdrawalService> _logger;

        private readonly string _cwUrl;
        private readonly string _cwOnUsUrl;
        private const string OnUsNbin = "608001";

        public FINOCashWithdrawalService(
            IFinoAepsApiClient api,
            IFinoAepsTransactionService txnService,
            IFinoAepsWalletService walletService,
            IFinoAepsCommissionService commissionService,
            IConfiguration config,
            ILogger<FINOCashWithdrawalService> logger)
        {
            _api                = api;
            _txnService         = txnService;
            _walletService      = walletService;
            _commissionService  = commissionService;
            _logger             = logger;

            var prod    = config.GetSection("FinoAEPS:Prod");
            _cwUrl      = prod["CashWithdrawalUrl"]      ?? "https://fpbs.fino.bank.in/AEPSAPIService/AEPSUIService.svc/ProcessRequest/AEPSCashWithdrawMerAuth";
            _cwOnUsUrl  = prod["CashWithdrawalOnUsUrl"]  ?? "https://fpbs.fino.bank.in/AEPSAPIService/AEPSUIService.svc/ProcessRequest/AEPSOnUsCashWithdraw";
        }

        public async Task<FinoAepsResponse> ProcessAsync(
            FinoAepsRequest request, string userId, string txnId,
            string lat, string lng, CancellationToken ct = default)
        {
            bool   onUs   = request.bankiinno == OnUsNbin;
            string url    = onUs ? _cwOnUsUrl : _cwUrl;
            string amount = string.IsNullOrWhiteSpace(request.amount) ? "0" : request.amount!;
            decimal txnAmount = decimal.TryParse(amount, out var a) ? a : 0m;

            string isNpciOtp = txnAmount > 5000m ? "1" : "0";
            if (isNpciOtp == "1" &&
                (string.IsNullOrWhiteSpace(request.npciTxnId) || string.IsNullOrWhiteSpace(request.npciTxnRefNo)))
                return Err("NPCI TransactionId and TxnReferenceNo are required for amounts above 5000");

            string bodyJson = JsonConvert.SerializeObject(new
            {
                MerchantID  = request.mobileno,
                Version     = "1001",
                ServiceID   = onUs ? "182" : "188",
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
                IMEI_MAC       = _api.GetMacAddress(),
                DeviceNo       = request.DeviceSrNo,
                CheckSum       = _api.ComputeChecksum($"{txnId}+{amount}+{request.aadharno}"),
                IsIris         = request.deviceType,
                MerAuthTxnId   = request.merAuthTxnId,
                IsNpciOtp      = isNpciOtp,
                TransactionId  = request.npciTxnId ?? "",
                TxnReferenceNo = request.npciTxnRefNo ?? ""
            });

            var commission = await _commissionService.CalculateCommissionAsync(int.Parse(userId), txnAmount, "CW", ct);

            var pendingRec = new FinoAepsTxnRecord(
                UserId      : int.Parse(userId),
                TxnId       : txnId,
                Mobile      : request.customermobileno ?? request.mobileno,
                Amount      : txnAmount,
                Status      : "Pending",
                TxnType     : "CW",
                Rrn         : "",
                AadhaarNo   : request.aadharno,
                BankName    : request.BankName,
                Brid        : txnId,
                ApiMsg      : "",
                ApiRes      : "",
                ApiReq      : bodyJson,
                Comm        : onUs ? 0 : commission.RetailerCommission,
                MdComm      : onUs ? 0 : commission.MdCommission,
                AdComm      : onUs ? 0 : commission.AdCommission,
                WlComm      : onUs ? 0 : commission.WlCommission,
                Tds         : onUs ? 0 : commission.Tds,
                Cost        : onUs ? Convert.ToDecimal(request.amount) : commission.Cost,
                NewBal      : 0,
                IfscCode    : null,
                CustomerName: request.customermobileno ?? request.mobileno,
                OpId        : null,
                OperatorName: "FINO_AEPS_CASH_WITHDRAWAL",
                ComingFrom: request.comingFrom,
                charge : 0
            );

            bool pendingInserted = await _txnService.InsertPendingAsync(pendingRec, ct);
            if (!pendingInserted)
                return Err("Failed to insert pending transaction");

            var result = await _api.PostProdAsync(url, bodyJson, ct);

            string status = result.IsPending ? "PENDING" : result.IsSuccess ? (result.DecryptedData?["Status"]?.ToString() ?? "SUCCESS") : "FAILED";
            string rrn    = result.DecryptedData?["RRN"]?.ToString() ?? "NA";

            decimal newBal = 0;
            if (result.IsSuccess && (status.Equals("Success", StringComparison.OrdinalIgnoreCase) || status.Equals("SUCCESS", StringComparison.OrdinalIgnoreCase)))
            {
                decimal oldBal = await _walletService.GetLatestWalletBalanceAsync(int.Parse(userId), ct);
                newBal = oldBal + (onUs ? Convert.ToDecimal(request.amount) : commission.Cost);
                await _walletService.CreditAsync(int.Parse(userId), onUs? Convert.ToDecimal(request.amount) : commission.Cost, "CW", request.BankName ?? "", txnId, onUs, ct);
            }

            await _txnService.UpdateWithCommissionAsync(txnId, status, result.RawResponse, commission, newBal, rrn, onUs, Convert.ToDecimal(request.amount), ct);

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

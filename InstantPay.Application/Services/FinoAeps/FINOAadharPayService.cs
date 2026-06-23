using InstantPay.Application.Interfaces.FinoAeps;
using InstantPay.SharedKernel.RequestPayload.FinoAEPS;
using InstantPay.SharedKernel.Results;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace InstantPay.Application.Services.FinoAeps
{
    public class FINOAadharPayService : IFINOAadharPayService
    {
        private readonly IFinoAepsApiClient _api;
        private readonly IFinoAepsTransactionService _txnService;
        private readonly IFinoAepsWalletService _walletService;
        private readonly IFinoAepsCommissionService _commissionService;
        private readonly ILogger<FINOAadharPayService> _logger;

        public FINOAadharPayService(
            IFinoAepsApiClient api,
            IFinoAepsTransactionService txnService,
            IFinoAepsWalletService walletService,
            IFinoAepsCommissionService commissionService,
            ILogger<FINOAadharPayService> logger)
        {
            _api                = api;
            _txnService         = txnService;
            _walletService      = walletService;
            _commissionService  = commissionService;
            _logger             = logger;
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
                ServiceID   = "176",
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

            decimal charge;
            if (txnAmount >= 100 && txnAmount <= 1000)
            {
                charge = 3;
            }
            else if (txnAmount >= 1001 && txnAmount <= 10000)
            {
                charge = txnAmount * 0.35m / 100;
            }
            else
            {
                charge = 0;
            }

            decimal userbalance = await _walletService.GetLatestWalletBalanceAsync(Convert.ToInt32(userId));
            if (userbalance < charge)
            {
                return Err("Insuficient Balance, to made this transaction you should have minimum balance in wallet: "+ charge+ ".");
            }

            var pendingRec = new FinoAepsTxnRecord(
                UserId      : int.Parse(userId),
                TxnId       : txnId,
                Mobile      : request.customermobileno ?? request.mobileno,
                Amount      : txnAmount,
                Status      : "Pending",
                TxnType     : "AP",
                Rrn         : "",
                AadhaarNo   : request.aadharno,
                BankName    : request.BankName,
                Brid        : txnId,
                ApiMsg      : "",
                ApiRes      : "",
                ApiReq      : bodyJson,
                Comm        : 0,
                MdComm      : 0,
                AdComm      : 0,
                WlComm      : 0,
                Tds         : 0,
                Cost        : txnAmount+charge,
                NewBal      : 0,
                IfscCode    : null,
                CustomerName: request.customermobileno ?? request.mobileno,
                OpId        : null,
                OperatorName: "FINO_AEPS_AADHAR_PAY",
                ComingFrom: request.comingFrom,
                charge: charge
            );

            bool pendingInserted = await _txnService.InsertPendingAsync(pendingRec, ct);
            if (!pendingInserted)
                return Err("Failed to insert pending transaction");

            var result = await _api.PostAadharPayProdAsync(bodyJson, ct);

            string status = result.IsSuccess ? (result.DecryptedData?["Status"]?.ToString() ?? "SUCCESS") : "FAILED";
            string rrn    = result.DecryptedData?["RRN"]?.ToString() ?? "NA";

            decimal newBal = 0;
            if (result.IsSuccess && (status.Equals("Success", StringComparison.OrdinalIgnoreCase) || status.Equals("SUCCESS", StringComparison.OrdinalIgnoreCase) || status.Equals("Pending", StringComparison.OrdinalIgnoreCase) || status.Equals("PENDING", StringComparison.OrdinalIgnoreCase)))
            {
                await _walletService.DebitAsync(int.Parse(userId), charge, charge,charge, "AP Charge Debit", request.BankName ?? "", txnId, ct);
                newBal = await _walletService.CreditAsync(int.Parse(userId), txnAmount, "AP", request.BankName ?? "", txnId, ct);
            }

            await _txnService.UpdateWithCommissionAsync(txnId, status, result.RawResponse, null, newBal, rrn, ct);

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

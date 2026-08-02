using System.ComponentModel.DataAnnotations;

namespace InstantPay.Application.DTOs;

public class GeneratePPIOtpRequest
{
    [Required]
    public string UserId { get; set; } = string.Empty;

    [Required]
    public string SenderMobile { get; set; } = string.Empty;

    [Required]
    public string APIKey { get; set; } = string.Empty;

    [Required]
    [RegularExpression(@"^\d{6}$", ErrorMessage = "Pincode must be exactly 6 digits")]
    public string Pincode { get; set; } = string.Empty;

    [Required]
    public string RTName { get; set; } = string.Empty;
}

public class GeneratePPIOtpResponse
{
    public string Status_Code { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Data { get; set; } = string.Empty;
}

public class VerifyPPIOtpRequest
{
    [Required]
    public string UserId { get; set; } = string.Empty;

    [Required]
    public string OTPToken { get; set; } = string.Empty;

    [Required]
    public string APIKey { get; set; } = string.Empty;

    [Required]
    public string OTP { get; set; } = string.Empty;
}

public class PPIWalletDetail
{
    public string SenderName { get; set; } = string.Empty;
    public string WalletStatus { get; set; } = string.Empty;
    public string TokeyKey { get; set; } = string.Empty;
    public string ApplicationNumber { get; set; } = string.Empty;
    public string WalletLimit { get; set; } = string.Empty;
    public string walletCurrentBalance { get; set; } = string.Empty;
}

public class VerifyPPIOtpResponse
{
    public string Status_Code { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public List<PPIWalletDetail> Data { get; set; } = new();
}

public class PPIBeneListRequest
{
    [Required]
    public string UserId { get; set; } = string.Empty;

    [Required]
    public string SenderMobile { get; set; } = string.Empty;

    [Required]
    public string APIKey { get; set; } = string.Empty;

    [Required]
    public string TokeyKey { get; set; } = string.Empty;
}

public class PPIBeneficiary
{
    public int beneId { get; set; }
    public string beneficiaryMobile { get; set; } = string.Empty;
    public string beneficiaryName { get; set; } = string.Empty;
    public string ifsCcode { get; set; } = string.Empty;
    public string accountNo { get; set; } = string.Empty;
    public int bankid { get; set; }
    public string bank { get; set; } = string.Empty;
    public int isAcValidate { get; set; }
    public int is_otp_required { get; set; }
    public string? otpToken { get; set; }
    public int imps { get; set; }
    public int neft { get; set; }
    public int isCoolingPeriod { get; set; }
}

public class PPIBeneListResponse
{
    public string Status_Code { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public List<PPIBeneficiary> Data { get; set; } = new();
}

public class PPIAddBeneRequest
{
    [Required]
    public string UserId { get; set; } = string.Empty;

    [Required]
    public string SenderMobile { get; set; } = string.Empty;

    [Required]
    public string APIKey { get; set; } = string.Empty;

    [Required]
    public string TokeyKey { get; set; } = string.Empty;

    [Required]
    public string BeneName { get; set; } = string.Empty;

    [Required]
    public string AccountNo { get; set; } = string.Empty;

    [Required]
    public string IfscCode { get; set; } = string.Empty;

    [Required]
    public string BankName { get; set; } = string.Empty;
}

public class PPIAddBeneResponse
{
    public string Status_Code { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Data { get; set; } = string.Empty;
}

public class PPIResendOtpRequest
{
    [Required]
    public string UserId { get; set; } = string.Empty;

    [Required]
    public string otptoken { get; set; } = string.Empty;

    [Required]
    public string APIKey { get; set; } = string.Empty;

    [Required]
    public string tokenkey { get; set; } = string.Empty;
}

public class PPIResendOtpResponse
{
    public string Status_Code { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Data { get; set; } = string.Empty;
}

public class PPIValidateOtpRequest
{
    [Required]
    public string UserId { get; set; } = string.Empty;

    [Required]
    public string otp { get; set; } = string.Empty;

    [Required]
    public string APIKey { get; set; } = string.Empty;

    [Required]
    public string otptoken { get; set; } = string.Empty;

    [Required]
    public string tokenkey { get; set; } = string.Empty;
}

public class PPIValidateOtpResponse
{
    public string Status_Code { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Data { get; set; } = string.Empty;
}

public class PPIDeleteOtpRequest
{
    [Required]
    public string UserId { get; set; } = string.Empty;

    [Required]
    public string mobilenumber { get; set; } = string.Empty;

    [Required]
    public string beneficiaryid { get; set; } = string.Empty;

    [Required]
    public string APIKey { get; set; } = string.Empty;

    [Required]
    public string tokenkey { get; set; } = string.Empty;
}

public class PPIDeleteOtpResponse
{
    public string Status_Code { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Data { get; set; } = string.Empty;
}

public class PPIDeleteVerifyRequest
{
    [Required]
    public string UserId { get; set; } = string.Empty;

    [Required]
    public string mobilenumber { get; set; } = string.Empty;

    [Required]
    public string otpToken { get; set; } = string.Empty;

    [Required]
    public string otp { get; set; } = string.Empty;

    [Required]
    public string APIKey { get; set; } = string.Empty;

    [Required]
    public string tokenkey { get; set; } = string.Empty;
}

public class PPIDeleteVerifyResponse
{
    public string Status_Code { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Data { get; set; } = string.Empty;
}

public class PPIAadharOtpRequest
{
    [Required]
    public string UserId { get; set; } = string.Empty;

    [Required]
    public string APIKey { get; set; } = string.Empty;

    [Required]
    public string TokeyKey { get; set; } = string.Empty;

    [Required]
    public string AadharNo { get; set; } = string.Empty;

    [Required]
    public string ConsentId { get; set; } = string.Empty;

    [Required]
    public string ApplicationNumber { get; set; } = string.Empty;

    [Required]
    public string pincode { get; set; } = string.Empty;

    [Required]
    public string RTName { get; set; } = string.Empty;
}

public class PPIAadharOtpResponse
{
    public string Status_Code { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Data { get; set; } = string.Empty;
}

public class PPIValidateAadharOtpRequest
{
    [Required]
    public string UserId { get; set; } = string.Empty;

    [Required]
    public string APIKey { get; set; } = string.Empty;

    [Required]
    public string AadharToken { get; set; } = string.Empty;

    [Required]
    public string ApplicationNumber { get; set; } = string.Empty;

    [Required]
    public string OTP { get; set; } = string.Empty;

    [Required]
    public string SenderMobile { get; set; } = string.Empty;

    [Required]
    public string TokeyKey { get; set; } = string.Empty;
}

public class PPIValidateAadharOtpResponse
{
    public string Status_Code { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Data { get; set; } = string.Empty;
}

public class PPIAadharBiometricRequest
{
    [Required]
    public string UserId { get; set; } = string.Empty;

    [Required]
    public string APIKey { get; set; } = string.Empty;

    [Required]
    public string TokeyKey { get; set; } = string.Empty;

    [Required]
    public string ApplicationNumber { get; set; } = string.Empty;

    [Required]
    public string pincode { get; set; } = string.Empty;

    [Required]
    public string RTName { get; set; } = string.Empty;

    [Required]
    public string AadharNo { get; set; } = string.Empty;

    [Required]
    public string SenderMobile { get; set; } = string.Empty;

    [Required]
    public string latitude { get; set; } = string.Empty;

    [Required]
    public string longitude { get; set; } = string.Empty;

    [Required]
    public string biometricdata { get; set; } = string.Empty;

    [Required]
    public string ConsentId { get; set; } = string.Empty;
}

public class PPIAadharBiometricResponse
{
    public string Status_Code { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Data { get; set; } = string.Empty;
}

public class PPIPanRequest
{
    [Required]
    public string UserId { get; set; } = string.Empty;

    [Required]
    public string APIKey { get; set; } = string.Empty;

    [Required]
    public string TokeyKey { get; set; } = string.Empty;

    [Required]
    public string PancardNo { get; set; } = string.Empty;

    [Required]
    public string ApplicationNumber { get; set; } = string.Empty;

    [Required]
    public string pincode { get; set; } = string.Empty;

    [Required]
    public string RTName { get; set; } = string.Empty;
}

public class PPIPanResponse
{
    public string Status_Code { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Data { get; set; } = string.Empty;
}

public class PPISendPaymentOtpRequest
{
    [Required]
    public string UserId { get; set; } = string.Empty;

    [Required]
    public string APIKey { get; set; } = string.Empty;

    [Required]
    public string TokeyKey { get; set; } = string.Empty;

    [Required]
    public string SenderMobile { get; set; } = string.Empty;

    [Required]
    public string BeneId { get; set; } = string.Empty;

    [Required]
    public string Amount { get; set; } = string.Empty;

    [Required]
    public string AccountNo { get; set; } = string.Empty;

    [Required]
    public string Ifsccode { get; set; } = string.Empty;
}

public class PPISendPaymentOtpResponse
{
    public string Status_Code { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Data { get; set; } = string.Empty;
}

public class PPIMoneyTransferRequest
{
    [Required]
    public string UserId { get; set; } = string.Empty;

    [Required]
    public string APIKey { get; set; } = string.Empty;

    [Required]
    public string TokeyKey { get; set; } = string.Empty;

    [Required]
    public string Sendermobile { get; set; } = string.Empty;

    [Required]
    public string BeneName { get; set; } = string.Empty;

    [Required]
    public string AccountNo { get; set; } = string.Empty;

    [Required]
    public string IfscCode { get; set; } = string.Empty;

    [Required]
    public string BeneId { get; set; } = string.Empty;

    [Required]
    public string Amount { get; set; } = string.Empty;

    [Required]
    public string TXNMode { get; set; } = string.Empty;

    [Required]
    public string BankName { get; set; } = string.Empty;

    [Required]
    public string OtpToken { get; set; } = string.Empty;

    [Required]
    public string OTP { get; set; } = string.Empty;

    public string ComingFrom { get; set; } = "APP";
}

public class PPIMoneyTransferTransaction
{
    public string AccountNo { get; set; } = string.Empty;
    public string BeneName { get; set; } = string.Empty;
    public string Amount { get; set; } = string.Empty;
    public string Charge { get; set; } = string.Empty;
    public string CurrentBalance { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string TxnID { get; set; } = string.Empty;
    public string BR_Id { get; set; } = string.Empty;
    public string TxnDate { get; set; } = string.Empty;
}

public class PPIMoneyTransferResponse
{
    public string Status_Code { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public object Data { get; set; } = string.Empty;
}

public class PPIFundTransferOtpRequest
{
    [Required]
    public string UserId { get; set; } = string.Empty;

    [Required]
    public string APIKey { get; set; } = string.Empty;

    [Required]
    public string TokeyKey { get; set; } = string.Empty;

    [Required]
    public string MobileNumber { get; set; } = string.Empty;

    [Required]
    public string BankAccountNumber { get; set; } = string.Empty;

    [Required]
    public string IFSCCode { get; set; } = string.Empty;

    [Required]
    public string BeneficiaryId { get; set; } = string.Empty;

    [Required]
    public string Amount { get; set; } = string.Empty;
}

public class PPIFundTransferOtpResponse
{
    public string Status_Code { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Data { get; set; } = string.Empty;
}

public class PPILoadWalletRequest
{
    [Required]
    public string UserId { get; set; } = string.Empty;

    [Required]
    public string APIKey { get; set; } = string.Empty;

    [Required]
    public string Sendermobile { get; set; } = string.Empty;

    [Required]
    public string Amount { get; set; } = string.Empty;

    [Required]
    public string TokeyKey { get; set; } = string.Empty;
    [Required]
    public string ComingFrom { get; set; } = string.Empty;
    [Required]
    public string TxnPin { get; set; } = string.Empty;
}

public class PPILoadWalletTransaction
{
    public string AccountNo { get; set; } = string.Empty;
    public string BeneName { get; set; } = string.Empty;
    public string Amount { get; set; } = string.Empty;
    public string Charge { get; set; } = string.Empty;
    public string CurrentBalance { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string TxnID { get; set; } = string.Empty;
    public string BR_Id { get; set; } = string.Empty;
    public string TxnDate { get; set; } = string.Empty;
}

public class PPILoadWalletResponse
{
    public string Status_Code { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public List<PPILoadWalletTransaction> Data { get; set; } = new();
}

public class PPICreateWalletRequest
{
    [Required]
    public string UserId { get; set; } = string.Empty;

    [Required]
    public string APIKey { get; set; } = string.Empty;

    [Required]
    public string TokeyKey { get; set; } = string.Empty;

    [Required]
    public string WalletAcCreatorCode { get; set; } = string.Empty;

    [Required]
    public string WalletAcCreatorName { get; set; } = string.Empty;

    [Required]
    [RegularExpression(@"^\d{6}$", ErrorMessage = "Pincode must be exactly 6 digits")]
    public string WalletAcCreatorPinCode { get; set; } = string.Empty;

    [Required]
    [RegularExpression(@"^\d{10}$", ErrorMessage = "Mobile number must be exactly 10 digits")]
    public string MobileNumber { get; set; } = string.Empty;

    [Required]
    [RegularExpression(@"^\d+$", ErrorMessage = "Application number must be a positive integer")]
    public string WalletAcApplicationNumber { get; set; } = string.Empty;

    [Required]
    [RegularExpression(@"^[A-Z0-9]{10}$", ErrorMessage = "PAN card number must be exactly 10 alphanumeric characters")]
    public string PancardNumber { get; set; } = string.Empty;

    [Required]
    public string PartnerTxnRefId { get; set; } = string.Empty;
}

public class PPICreateWalletResult
{
    public bool PancardVerified { get; set; }
    public bool PancardPhotoRequired { get; set; }
    public bool WalletCreated { get; set; }
    public string WalletHolderName { get; set; } = string.Empty;
    public string KycType { get; set; } = string.Empty;
    public string AccountStatus { get; set; } = string.Empty;
    public string CashTopUpLimitAvailable { get; set; } = string.Empty;
    public string CashTopUpLimitConsumed { get; set; } = string.Empty;
}

public class PPICreateWalletResponse
{
    public string ResultCode { get; set; } = string.Empty;
    public string ResultStatus { get; set; } = string.Empty;
    public string ResultMessage { get; set; } = string.Empty;
    public PPICreateWalletResult? Result { get; set; }
}

using InstantPay.SharedKernel.Results.MoneyTransfer.RBL;

namespace InstantPay.Application.Services.RBL;

internal static class RblPaymentResponseClassifier
{
    public static string GetStatus(RblResponseHeader? header)
    {
        var providerStatus = header?.Status?.Trim();
        if (string.Equals(providerStatus, "Success", StringComparison.OrdinalIgnoreCase))
            return "SUCCESS";
        if (string.Equals(providerStatus, "Failed", StringComparison.OrdinalIgnoreCase))
            return "FAILED";
        return "PENDING";
    }
}

using System.Text.RegularExpressions;

namespace InstantPay.Application.Services.RBL;

public static partial class RblPayloadNormalizer
{
    private const int BankNameMaxLength = 20;

    public static string NormalizeBankName(string? bankName)
    {
        var value = WhitespaceRegex().Replace(bankName?.Trim() ?? string.Empty, " ");
        if (value.Length <= BankNameMaxLength) return value;

        // Use recognizable banking abbreviations before applying the hard schema cap.
        value = ReplaceWord(value, "FINANCE", "FIN");
        value = ReplaceWord(value, "LIMITED", "LTD");
        value = ReplaceWord(value, "PRIVATE", "PVT");
        value = value.Replace("CO-OPERATIVE", "COOP", StringComparison.OrdinalIgnoreCase);
        value = WhitespaceRegex().Replace(value, " ").Trim();

        return value.Length <= BankNameMaxLength
            ? value
            : value[..BankNameMaxLength].TrimEnd();
    }

    private static string ReplaceWord(string value, string word, string replacement) =>
        Regex.Replace(value, $@"\b{Regex.Escape(word)}\b", replacement,
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();
}

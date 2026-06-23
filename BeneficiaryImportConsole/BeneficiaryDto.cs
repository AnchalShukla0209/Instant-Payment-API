namespace BeneficiaryImportConsole;

public class BeneficiaryDto
{
    public bool Status { get; set; }
    public string Name { get; set; } = string.Empty;
    public string AccountNumber { get; set; } = string.Empty;
    public string BankName { get; set; } = string.Empty;
    public string Ifsc { get; set; } = string.Empty;
    public string CustomerNumber { get; set; } = string.Empty;
    public DateTime CreatedOn { get; set; }
    public DateTime UpdatedOn { get; set; }
}

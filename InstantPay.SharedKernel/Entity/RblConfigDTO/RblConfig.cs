namespace InstantPay.SharedKernel.Entity.RblConfigDTO;

public sealed class RblConfig
{
    public string PaymentUrl { get; set; } = string.Empty;
    public string StatementUrl { get; set; } = string.Empty;
    public string StatementWrapperUrl { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string CertificatePath { get; set; } = string.Empty;
    public string CertificatePassword { get; set; } = string.Empty;
    public string CorpId { get; set; } = string.Empty;
    public string MakerId { get; set; } = string.Empty;
    public string CheckerId { get; set; } = string.Empty;
    public string ApproverId { get; set; } = string.Empty;
    public string DebitAccountNumber { get; set; } = string.Empty;
    public string DebitAccountName { get; set; } = string.Empty;
    public string DebitIfsc { get; set; } = string.Empty;
    public string DebitMobile { get; set; } = string.Empty;
    public string NodeExecutable { get; set; } = "node";
}

using Newtonsoft.Json;
using System.ComponentModel.DataAnnotations;

namespace InstantPay.SharedKernel.RequestPayload.RBL;

public sealed class RblDateRangeStatementRequest
{
    [JsonProperty("Acc_Stmt_DtRng_Req")]
    [Required]
    public RblDateRangeEnvelope Request { get; set; } = new();
}

public sealed class RblPeriodStatementRequest
{
    [JsonProperty("Acc_Stmt_Period_Req")]
    [Required]
    public RblPeriodEnvelope Request { get; set; } = new();
}

public sealed class RblDateRangeEnvelope
{
    public RblStatementHeader Header { get; set; } = new();
    [Required] public RblDateRangeBody Body { get; set; } = new();
    public RblStatementSignature Signature { get; set; } = new();
}

public sealed class RblPeriodEnvelope
{
    public RblStatementHeader Header { get; set; } = new();
    [Required] public RblPeriodBody Body { get; set; } = new();
    public RblStatementSignature Signature { get; set; } = new();
}

public sealed class RblStatementHeader
{
    public string TranID { get; set; } = string.Empty;
    public string Corp_ID { get; set; } = string.Empty;
    public string Approver_ID { get; set; } = string.Empty;
}

public sealed class RblDateRangeBody
{
    public string Acc_No { get; set; } = string.Empty;
    [Required, RegularExpression("^[DCB]$", ErrorMessage = "Tran_Type must be D, C, or B")]
    public string Tran_Type { get; set; } = "B";
    [Required] public string From_Dt { get; set; } = string.Empty;
    public RblPaginationDetails Pagination_Details { get; set; } = new();
    [Required] public string To_Dt { get; set; } = string.Empty;
}

public sealed class RblPeriodBody
{
    public string Acc_No { get; set; } = string.Empty;
    [Required, RegularExpression("^[DCB]$", ErrorMessage = "Tran_Type must be D, C, or B")]
    public string Tran_Type { get; set; } = "B";
    [Required, RegularExpression("^[MQHY]$", ErrorMessage = "Period must be M, Q, H, or Y")]
    public string Period { get; set; } = "M";
    public RblPaginationDetails Pagination_Details { get; set; } = new();
}

public sealed class RblPaginationDetails
{
    public RblLastBalance Last_Balance { get; set; } = new();
    public string Last_Pstd_Date { get; set; } = string.Empty;
    public string Last_Txn_Date { get; set; } = string.Empty;
    public string Last_Txn_Id { get; set; } = string.Empty;
    public string Last_Txn_SrlNo { get; set; } = string.Empty;
}

public sealed class RblLastBalance
{
    public string Amount_Value { get; set; } = string.Empty;
    public string Currency_Code { get; set; } = string.Empty;
}

public sealed class RblStatementSignature
{
    public string Signature { get; set; } = "Signature";
}

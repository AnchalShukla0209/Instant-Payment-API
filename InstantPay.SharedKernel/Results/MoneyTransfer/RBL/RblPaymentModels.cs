using Newtonsoft.Json;

namespace InstantPay.SharedKernel.Results.MoneyTransfer.RBL;

public sealed class RblPaymentResponse
{
    [JsonProperty("Single_Payment_Corp_Resp")]
    public RblPaymentResponseEnvelope? Payment { get; set; }
}

public sealed class RblPaymentResponseEnvelope
{
    [JsonProperty("Header")]
    public RblResponseHeader? Header { get; set; }
    [JsonProperty("Body")]
    public RblResponseBody? Body { get; set; }
}

public sealed class RblResponseHeader
{
    public string? TranID { get; set; }
    public string? Status { get; set; }
    public string? Resp_cde { get; set; }
    public string? Error_Cde { get; set; }
    public string? Error_Desc { get; set; }
}

public sealed class RblResponseBody
{
    public string? RefNo { get; set; }
    public string? channelpartnerrefno { get; set; }
    public string? RRN { get; set; }
    public string? Txn_Time { get; set; }
}

namespace InstantPay.Application.Interfaces.FinoAeps
{
    public record FinoAepsTxnRecord(
        int UserId,
        string TxnId,
        string? Mobile,
        decimal Amount,
        string Status,
        string TxnType,
        string Rrn,
        string? AadhaarNo,
        string? BankName,
        string Brid,
        string? ApiMsg,
        string? ApiRes,
        string? ApiReq,
        decimal Comm = 0,
        decimal MdComm = 0,
        decimal AdComm = 0,
        decimal WlComm = 0,
        decimal Tds = 0,
        decimal Cost = 0,
        decimal NewBal = 0,
        string? IfscCode = null,
        string? CustomerName = null,
        string? OpId = null,
        string? OperatorName = null,
        string? ComingFrom = null,
        decimal? charge = 0
    );
}

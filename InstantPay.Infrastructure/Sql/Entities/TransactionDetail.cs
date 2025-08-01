using System;
using System.Collections.Generic;

namespace InstantPay.Infrastructure.Sql.Entities;

public partial class TransactionDetail
{
    public int TransId { get; set; }

    public string? UserId { get; set; }

    public string? UserName { get; set; }

    public string? WlId { get; set; }

    public string? MdId { get; set; }

    public string? AdId { get; set; }

    public string? TxnId { get; set; }

    public string? ServiceName { get; set; }

    public string? OperatorName { get; set; }

    public string? OpId { get; set; }

    public string? Mobileno { get; set; }

    public decimal? OldBal { get; set; }

    public decimal? Amount { get; set; }

    public decimal? Comm { get; set; }

    public decimal? Charge { get; set; }

    public decimal? Cost { get; set; }

    public string? NewBal { get; set; }

    public string? Status { get; set; }

    public string? Brid { get; set; }

    public string? TxnType { get; set; }

    public string? ApiTxnId { get; set; }

    public string? ApiName { get; set; }

    public string? AdminRemarks { get; set; }

    public string? ApiMsg { get; set; }

    public string? ApiRes { get; set; }

    public string? ApiReq { get; set; }

    public DateTime? ReqDate { get; set; }

    public DateTime? UpdateDate { get; set; }

    public string? CustomerName { get; set; }

    public string? AccountNo { get; set; }

    public string? ComingFrom { get; set; }

    public string? IfscCode { get; set; }

    public string? BankName { get; set; }

    public decimal? Tds { get; set; }

    public string? TxnMode { get; set; }

    public decimal? MdComm { get; set; }

    public decimal? AdComm { get; set; }

    public decimal? WlComm { get; set; }
    public int? ServiceId { get; set; }
}

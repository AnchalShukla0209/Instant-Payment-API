using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InstantPay.SharedKernel.Results.MoneyTransfer.Castler
{
    public class ApiResponse<T>
    {
        public int Code { get; set; }
        public string? Message { get; set; }
        public T? Data { get; set; }
    }

    public class CastlerTransferResponse
    {
        public bool Success { get; set; }
        public CastlerTransferResult Result { get; set; }
        public List<string> Errors { get; set; }
    }

    public class CastlerTransferResult
    {
        public string Status { get; set; }
        public string TransferId { get; set; }
        public string CustomerRefId { get; set; }
        public string? utr { get; set; }
    }

    public class PayeeResponse
    {
        public bool Success { get; set; }
        public PayeeResult Result { get; set; }
        public List<string> Errors { get; set; }
    }

    public class PayeeResult
    {
        public string PayeeId { get; set; }
    }

    public class PayeeListResponse
    {
        [JsonProperty("success")]
        public bool Success { get; set; }
        [JsonProperty("result")]
        public List<PayeeListResult> Result { get; set; }
        [JsonProperty("errors")]
        public List<string> Errors { get; set; }
    }

    public class PayeeListResult
    {
        public string PayeeId { get; set; }
        public string AccountNumber { get; set; }
    }

    public class DmtTransactionResult
    {
        public string TransactionId { get; set; }
        public string Status { get; set; }
    }

    public class DMTTXN
    {
        public string? AccountNo { get; set; }
        public string? BeneName { get; set; }
        public string? Amount { get; set; }
        public string? Charge { get; set; }
        public string? CurrentBalance { get; set; }
        public string? Status { get; set; }
        public string? TxnID { get; set; }
        public string? BR_Id { get; set; }
        public string? TxnDate { get; set; }
    }

    public class LoginModel
    {
        public string? Status_Code { get; set; }
        public string? Message { get; set; }
        public object Data { get; set; }
    }
}

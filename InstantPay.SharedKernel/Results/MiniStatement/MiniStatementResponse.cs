using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InstantPay.SharedKernel.Results.MiniStatement
{
    public class MiniStatementResponse
    {
        [JsonProperty("responseCode")]
        public string ResponseCode { get; set; }

        [JsonProperty("responseMessage")]
        public string ResponseMessage { get; set; }

        [JsonProperty("responseData")]
        public MiniStatementResponseData ResponseData { get; set; }

        [JsonProperty("errorCode")]
        public string ErrorCode { get; set; }

        [JsonProperty("errorMessage")]
        public string ErrorMessage { get; set; }

        [JsonProperty("traceId")]
        public string TraceId { get; set; }

        [JsonProperty("timestamp")]
        public string Timestamp { get; set; }
    }

    public class MiniStatementResponseData
    {
        [JsonProperty("transaction")]
        public MiniStatementTransaction Transaction { get; set; }

        [JsonProperty("account")]
        public MiniStatementAccount Account { get; set; }

        [JsonProperty("miniStatement")]
        public List<MiniStatementItem> MiniStatement { get; set; }
    }

    public class MiniStatementTransaction
    {
        [JsonProperty("transactionTime")]
        public string TransactionTime { get; set; }

        [JsonProperty("rrn")]
        public string Rrn { get; set; }

        [JsonProperty("transactionId")]
        public string TransactionId { get; set; }
    }

    public class MiniStatementAccount
    {
        [JsonProperty("balance")]
        public string Balance { get; set; }
    }

    public class MiniStatementItem
    {
        [JsonProperty("transactionType")]
        public string TransactionType { get; set; }

        [JsonProperty("transactionTime")]
        public string TransactionTime { get; set; }

        [JsonProperty("amount")]
        public string Amount { get; set; }

        [JsonProperty("transactionDetails")]
        public string TransactionDetails { get; set; }
    }

}

using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InstantPay.SharedKernel.Results.CashWithdrawal
{
    public class CashWithdrawalResponseData
    {
        [JsonProperty("transaction")]
        public CashWithdrawalTransaction Transaction { get; set; }

        [JsonProperty("account")]
        public CashWithdrawalAccount Account { get; set; }
    }

    public class CashWithdrawalTransaction
    {
        [JsonProperty("transactionTime")]
        public string TransactionTime { get; set; }

        [JsonProperty("rrn")]
        public string Rrn { get; set; }

        [JsonProperty("transactionId")]
        public string TransactionId { get; set; }
    }

    public class CashWithdrawalAccount
    {
        [JsonProperty("balance")]
        public string Balance { get; set; }

        [JsonProperty("accountExist")]
        public bool AccountExist { get; set; }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using static InstantPay.SharedKernel.Enums.WalletOperationStatusENUM;

namespace InstantPay.SharedKernel.Entity
{
    public class WalletTransactionRequest
    {
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public WalletOperationStatus Status { get; set; }
        public string TxnPin { get; set; } = null!;
        public decimal Amount { get; set; }
        public int UserId { get; set; }
        public int ActionById { get; set; }
    }

    public class WalletTransactionResponse
    {
        public string ErrorMessage { get; set; } = null!;
        public bool IsSuccessful { get; set; }
    }

}

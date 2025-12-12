using MediatR;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InstantPay.SharedKernel.Results
{
    public class ResponseSuccess
    {
        public bool? success { get; set; }
        public string? message { get; set; }
        public string? txnid { get; set; }
        public string? apitxnid { get; set; }
        public string? transactiondatetime { get; set; }
    }

    public class JIOAgentLoginResponse
    {
        public bool? success { get; set; }
        public string message { get; set; }
        public string txnid { get; set; }
        public string apitxnid { get; set; }
        public string transactiondatetime { get; set; }
        public string AgentLoginId { get; set; }
        public string AgentPinCode { get; set; }
        public double Lattitude { get; set; }
        public double Longtitude { get; set; }
        public string StatusCode { get; set; }
        public string aepsauthtoken { get; set; }
        public string appidentifiertoken { get; set; }
        public string agentrefno { get; set; }
    }

    public class TransactionDetailsDto
    {
        public string? UserName { get; set; }
        public string TxnId { get; set; }
        public string? ServiceName { get; set; }
        public decimal OldBal { get; set; }
        public decimal Amount { get; set; }
        public decimal NewBal { get; set; }
    }


    public class GetLatestTransactionsQuery 
    {
        public string UserId { get; set; }
        public string? UserName { get; set; }
    }

    public class WalletBalanceDto
    {
        public int UserId { get; set; }
        public string UserName { get; set; } = string.Empty;
        public decimal Balance { get; set; }
    }

    public class GetWalletBalanceRequest
    {
        public int UserId { get; set; }
        public string UserName { get; set; } = string.Empty;
    }




}

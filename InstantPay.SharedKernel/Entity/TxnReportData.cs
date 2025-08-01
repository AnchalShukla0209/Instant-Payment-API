using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InstantPay.SharedKernel.Entity
{

    public class TxnReportData
    {
        [Key]
        public int Id { get; set; }
        public string? TXN_ID { get; set; }
        public string? BankRefNo { get; set; }
        public string? UserName { get; set; }
        public string? OperatorName { get; set; }
        public string? AccountNo { get; set; }
        public decimal? OpeningBal { get; set; }
        public decimal? Amount { get; set; }
        public decimal? Closing { get; set; }
        public string? Status { get; set; }
        public string? APIName { get; set; }
        public string? ComingFrom { get; set; }
        public string? MasterDistributor { get; set; }
        public string? Distributor { get; set; }
        public DateTime? TimeStamp { get; set; }
        public DateTime? UpdatedTime { get; set; }
        public string? Success { get; set; }
        public string? Failed { get; set; }
        public string? APIRes { get; set; }
        public int? TotalTransactions { get; set; }
        public decimal? TotalAmount { get; set; }
        public int? flagforTrans { get; set; }
    }

    public class PaginatedTxnResultDto
    {
        public List<TxnReportData> Data { get; set; } = new();
        public int TotalTransactions { get; set; }
        public decimal TotalAmount { get; set; }
        public int FlagForTrans { get; set; } = 0;
    }
    public class TxnReportPayload
    {
        public string? serviceType { get; set; } = "";
        public string? status { get; set; } = "";
        public string? dateFrom { get; set; } = "";
        public string? dateTo { get; set; } = "";
        public int? userId { get; set; } = 0;
        public int? pageIndex { get; set; } = 0;
        public int? pageSize { get; set; } = 0;
    }


}

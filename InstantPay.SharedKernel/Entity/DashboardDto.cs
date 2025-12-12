using InstantPay.SharedKernel.Results;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InstantPay.SharedKernel.Entity
{
    public class DashboardDto
    {
        public decimal WalletAmount { get; set; }
        public List<ServiceDto> Services { get; set; } = new();
        public long TotalTransaction { get; set; }
        public int UserJoined { get; set; }
        public List<TransactionDetailsDto> _TransactionDetails { get; set; }
    }

    public class ServiceDto
    {
        public int ServiceId { get; set; }
        public string ServiceName { get; set; } = string.Empty;
        public string ServiceImagePath { get; set; } = string.Empty;
        public bool? ActiveStatus { get; set; } 
    }

}

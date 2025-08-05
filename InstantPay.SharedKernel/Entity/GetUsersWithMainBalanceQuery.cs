using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InstantPay.SharedKernel.Entity
{
    public class GetUsersWithMainBalanceQuery
    {
        public int PageIndex { get; set; } = 1;
        public int PageSize { get; set; } = 50;
        public string? FromDate { get; set; }
        public string? ToDate { get; set; }
    }

    public class GetUsersWithMainBalanceResponse
    {
        public int TotalRecords { get; set; }
        public int PageIndex { get; set; }
        public int PageSize { get; set; }
        public int TotalPages => (int)Math.Ceiling((decimal)TotalRecords / PageSize);
        public decimal TotalBalance { get; set; }
        public List<UserBalanceDto> Users { get; set; }
    }

    public class UserBalanceDto
    {
        public int Id { get; set; }
        public string UserName { get; set; }
        public string CompanyName { get; set; }
        public string Domain { get; set; }
        public string City { get; set; }
        public string Status { get; set; }
        public string EmailId { get; set; }
        public decimal MainBalance { get; set; }
    }


}

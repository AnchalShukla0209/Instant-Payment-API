using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InstantPay.SharedKernel.Entity
{
    public class GetUsersWithMainBalanceQuery
    {
        public int pageIndex { get; set; } = 0;
        public int pageSize { get; set; } = 0;
        public string? fromDate { get; set; }
        public string? toDate { get; set; }
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
    public class CreateOrUpdateClientCommand
    {
        public int ClientId { get; set; }
        public string CompanyName { get; set; }
        public string UserName { get; set; }
        public string EmailId { get; set; }
        public string Phone { get; set; }
        public string Password { get; set; }
        public string PanCard { get; set; }
        public string AadharCard { get; set; }
        public string DomainName { get; set; }
        public string AddressLine1 { get; set; }
        public string AddressLine2 { get; set; }
        public string State { get; set; }
        public string City { get; set; }
        public string Pincode { get; set; }
        public string Recharge { get; set; }
        public string MoneyTransfer { get; set; }
        public string AEPS { get; set; }
        public string BillPayment { get; set; }
        public string MicroATM { get; set; }
        public string APITransfer { get; set; }
        public string Margin { get; set; }
        public string Debit { get; set; }
        public string Status { get; set; }
        public DateTime RegDate { get; set; }
        public string TxnPin { get; set; }
        public string PlanId { get; set; }
        public IFormFile? PancopyFile { get; set; }
        public IFormFile? AadharFrontFile { get; set; }
        public IFormFile? AadharBackFile { get; set; }
        public IFormFile? LogoFile { get; set; }
    }


    public class ResponseModelforClientaddandupdateapi
    {
        public int id { get; set; }
        public string Msg { get; set; }
        public bool flag { get; set; }
    }


    public class GetClientDetail
    {
        public int? Id { get; set; }
        public string? CompanyName { get; set; }
        public string? UserName { get; set; }
        public string? EmailId { get; set; }
        public string? Phone { get; set; }
        public string? Password { get; set; }
        public string? PanCard { get; set; }
        public string? AadharCard { get; set; }
        public string? DomainName { get; set; }
        public string? Logo { get; set; }
        public string? AddressLine1 { get; set; }
        public string? AddressLine2 { get; set; }
        public string? State { get; set; }
        public string? City { get; set; }
        public string? Pincode { get; set; }
        public string? Pancopy { get; set; }
        public string? AadharFront { get; set; }
        public string? AadharBack { get; set; }
        public string? Recharge { get; set; }
        public string? MoneyTransfer { get; set; }
        public string? AEPS { get; set; }
        public string? BillPayment { get; set; }
        public string? MicroATM { get; set; }
        public string? APITransfer { get; set; }
        public string? Margin { get; set; }
        public string? Debit { get; set; }
        public string? Status { get; set; }
        public DateTime? RegDate { get; set; }
        public string? TxnPin { get; set; }
        public string? PlanId { get; set; }
    }

    public class DeleteClientFileCommand 
    {
        public int ClientId { get; set; }
        public string FileType { get; set; } = string.Empty;
    }




}

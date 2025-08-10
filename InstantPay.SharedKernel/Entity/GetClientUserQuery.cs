using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InstantPay.SharedKernel.Entity
{
    public class GetClientUserQuery
    {
        public int pageIndex { get; set; } = 0;
        public int pageSize { get; set; } = 0;
        public string? fromDate { get; set; }
        public string? toDate { get; set; }
        public int? ClientId { get; set; }
    }

    public class GetClientUsersWithMainBalanceResponse
    {
        public int TotalRecords { get; set; }
        public int PageIndex { get; set; }
        public int PageSize { get; set; }
        public int TotalPages => (int)Math.Ceiling((decimal)TotalRecords / PageSize);
        public decimal TotalBalance { get; set; }
        public List<UserBalanceRec> Users { get; set; }
    }

    public class UserBalanceRec
    {
        public int Id { get; set; }
        public string UserName { get; set; }
        public string CompanyName { get; set; }
        public string Phone { get; set; }
        public DateTime CreatedDate { get; set; }
        public string UserType { get; set; }
        public string City { get; set; }
        public string Status { get; set; }
        public string EmailId { get; set; }
        public string PlanName { get; set; }
        public string MDName { get; set; }
        public string ADName { get; set; }
        public decimal MainBalance { get; set; }
    }
    public class CreateOrUpdateClientUserCommand
    {
        public int Id { get; set; }
        public int ClientId { get; set; }
        public string CompanyName { get; set; }
        public string UserName { get; set; }
        public string EmailId { get; set; }
        public string Phone { get; set; }
        public string Password { get; set; }
        public string PanCard { get; set; }
        public string AadharCard { get; set; }
        public string UserType { get; set; }
        public string CustomerName { get; set; }
        public string AddressLine1 { get; set; }
        public string AddressLine2 { get; set; }
        public string State { get; set; }
        public string City { get; set; }
        public string Pincode { get; set; }
        public string ShopAddress { get; set; }
        public string ShopState { get; set; }
        public string ShopCity { get; set; }
        public string ShopZipCode { get; set; }
        public string Recharge { get; set; }
        public string MoneyTransfer { get; set; }
        public string AEPS { get; set; }
        public string BillPayment { get; set; }
        public string MicroATM { get; set; }
        public string Status { get; set; }
        public string? lat { get; set; }
        public string? longitute { get; set; }
        public string TxnPin { get; set; }
        public string WLID { get; set; }
        public IFormFile? PancopyFile { get; set; }
        public IFormFile? AadharFrontFile { get; set; }
        public IFormFile? AadharBackFile { get; set; }
        public IFormFile? LogoFile { get; set; }
    }


    public class ResponseModelforClientUseraddandupdateapi
    {
        public int id { get; set; }
        public string Msg { get; set; }
        public bool flag { get; set; }
    }


    public class GetClientUserDetail
    {
        public int? Id { get; set; }
        public string? CompanyName { get; set; }
        public string? CustomerName { get; set; }
        public string? UserName { get; set; }
        public string? EmailId { get; set; }
        public string? UserType { get; set; }
        public string? Phone { get; set; }
        public string? Password { get; set; }
        public string? PanCard { get; set; }
        public string? AadharCard { get; set; }


        public string? Logo { get; set; }
        public string? AddressLine1 { get; set; }
        public string? AddressLine2 { get; set; }
        public string? State { get; set; }
        public string? City { get; set; }
        public string? Pincode { get; set; }



        public string? ShopAddress { get; set; }
        public string? ShopState { get; set; }
        public string? ShopCity { get; set; }
        public string? ShopZipCode { get; set; }
        public int? ClientId { get; set; }
        public string? MDName { get; set; }
        public string? ADName  { get; set; }
        public string? ADMINName  { get; set; }

        public string? Pancopy { get; set; }
        public string? AadharFront { get; set; }
        public string? AadharBack { get; set; }
        
        
        public string? Recharge { get; set; }
        public string? MoneyTransfer { get; set; }
        public string? AEPS { get; set; }
        public string? BillPayment { get; set; }
        public string? MicroATM { get; set; }
        public string? Status { get; set; }


        public DateTime? RegDate { get; set; }
        public string? TxnPin { get; set; }
    }

    public class DeleteClientUserFileCommand
    {
        public int ClientId { get; set; }
        public string FileType { get; set; } = string.Empty;
    }




}

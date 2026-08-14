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
        /// <summary>'AD' = Distributor scope (filter by Adid), 'MD' = Master Distributor scope (filter by Mdid), null/other = White-Label scope (filter by Wlid).</summary>
        public string? ScopeType { get; set; }
        public string? commonsearch { get; set; }
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

        public string? Name { get; set; }
        public string Status { get; set; }
        public string EmailId { get; set; }
        public string PlanName { get; set; }
        public string MDName { get; set; }
        public string ADName { get; set; }
        public decimal MainBalance { get; set; }
        public string? MPin { get; set; }
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
        public string RazorpayPayment { get; set; }
        public string Settlement { get; set; }
        public string Status { get; set; }
        public string? lat { get; set; }
        public string? longitute { get; set; }
        public string TxnPin { get; set; }
        public string WLID { get; set; }
        /// <summary>'AD' = Distributor scope (sets Adid = ScopePartnerId), 'MD' = Master Distributor scope (sets Mdid = ScopePartnerId), null/other = White-Label scope (sets Wlid = WLID).</summary>
        public string? ScopeType { get; set; }
        /// <summary>Logged-in AD/MD partner id (server-side). Used for Adid/Mdid while WLID carries the partner's white-label id.</summary>
        public int ScopePartnerId { get; set; }
        public string MPin { get; set; }
        public int CommissionPlanId { get; set; }
        public string? MobileVerificationToken { get; set; }
        public string? EmailVerificationToken { get; set; }
        public string? PanVerificationToken { get; set; }
        public string? AadharVerificationToken { get; set; }
        public IFormFile? PancopyFile { get; set; }
        public IFormFile? AadharFrontFile { get; set; }
        public IFormFile? AadharBackFile { get; set; }
        public IFormFile? LogoFile { get; set; }
        public IFormFile? SelfieFile { get; set; }
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
        public string? SelfieImage { get; set; }
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
        
        
        public string? MobileRecharge { get; set; }
        public string? MoneyTransfer { get; set; }
        public string? AEPS { get; set; }
        public string? BillPayment { get; set; }
        public string? MicroATM { get; set; }
        public string? RazorpayPayment { get; set; }
        public string? Settlement { get; set; }
        public string? Status { get; set; }
        public string? Lat { get; set; }
        public string? Longitute { get; set; }
        public int? CommissionPlanId { get; set; }
        public bool IsPhoneVerified { get; set; }
        public bool IsEmailVerified { get; set; }
        public bool IsPanVerified { get; set; }
        public string? PanVerifiedName { get; set; }
        public bool IsAadhaarVerified { get; set; }


        public DateTime? RegDate { get; set; }
        public string? TxnPin { get; set; }
        public string? MPin { get; set; }
    }

    public class DeleteClientUserFileCommand
    {
        public int ClientId { get; set; }
        public string FileType { get; set; } = string.Empty;
    }




}

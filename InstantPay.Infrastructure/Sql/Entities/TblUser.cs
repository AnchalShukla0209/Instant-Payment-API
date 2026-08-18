using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace InstantPay.Infrastructure.Sql.Entities;

public partial class TblUser
{
    public int Id { get; set; }

    public string? CompanyName { get; set; }

    public string? Name { get; set; }

    public string? FatherName { get; set; }

    public string? EmailId { get; set; }

    public string? Phone { get; set; }

    public string? Password { get; set; }

    public string? PanCard { get; set; }

    public string? AadharCard { get; set; }
    public bool IsAadhaarVerified { get; set; }
    public DateTime? AadharVerifiedAt { get; set; }
    public bool IsPhoneVerified { get; set; }
    public DateTime? PhoneVerifiedAt { get; set; }
    public bool IsEmailVerified { get; set; }
    public DateTime? EmailVerifiedAt { get; set; }
    public bool IsPanVerified { get; set; }
    public DateTime? PanVerifiedAt { get; set; }
    public string? PanVerifiedName { get; set; }
    public string? Wlid { get; set; }

    public string? AddressLine1 { get; set; }

    public string? AddressLine2 { get; set; }

    public string? State { get; set; }

    public string? City { get; set; }

    public string? Pincode { get; set; }

    public string? Pancopy { get; set; }

    public string? AadharFront { get; set; }

    public string? AadharBack { get; set; }

    [Column("Recharge")]
    public string? MobileRecharge { get; set; }

    public string? MoneyTransfer { get; set; }

    public string? Aeps { get; set; }
    public string? AepsStatus { get; set; }

    public string? BillPayment { get; set; }

    public string? MicroAtm { get; set; }
    public string? RazorpayPayment { get; set; }
    public string? Settlement { get; set; }
    public string? Status { get; set; }
    public string? PlanId { get; set; }

    public string? Lat { get; set; }
    public string? Longitute { get; set; }

    public string? DeviceId { get; set; }

    public string? TokenKey { get; set; }

    public string? DeviceInfo { get; set; }

    public DateTime? RegDate { get; set; }
    public string? SessionKey { get; set; }

    public string? Usertype { get; set; }

    public string? Mdid { get; set; }

    public string? Adid { get; set; }

    public string? Stid { get; set; }

    public string? Logo { get; set; }
    public string? SelfieImage { get; set; }


    public string? TxnPin { get; set; }
    public string? MerchargeCode { get; set; }

    public string? Username { get; set; }

    public string? ShopAddress { get; set; }

    public string? ShopState { get; set; }

    public string? ShopCity { get; set; }

    public string? ShipZipcode { get; set; }

    public string? Latlongstatus { get; set; }
    public string? MPin { get; set; }
    public string? ResetToken { get; set; }
    public DateTime? ResetTokenExpiry { get; set; }
    public string? ResetOtpHash { get; set; }
    public DateTime? ResetOtpExpiry { get; set; }
    public int? ResetOtpAttempts { get; set; }
    public DateTime? LastOtpSentAt { get; set; }

    public bool? IsUserLoggedInFromWeb { get; set; }
    public bool? IsUserLoggedInFromApk { get; set; }

    public int? FailedUnlockAttempts { get; set; }
    public DateTime? LockoutEnd { get; set; }

    public int? CommissionPlanId { get; set; }
    public int? SuperAdminId { get; set; }
    public string? OnboardingStatus { get; set; }
    public int OnboardingVersion { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public int? ApprovedBy { get; set; }
    public DateTime? RejectedAt { get; set; }
    public int? RejectedBy { get; set; }
    public string? FinalReviewRemarks { get; set; }
    public DateTime? LastDraftSavedAt { get; set; }
    public int? CreatedByUserId { get; set; }
    public string? CreatedByUserType { get; set; }
    public byte[]? RowVersion { get; set; }
}

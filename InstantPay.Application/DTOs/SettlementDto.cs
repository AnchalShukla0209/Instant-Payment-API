using System;
using System.Collections.Generic;

namespace InstantPay.Application.DTOs
{
    public class SettlementDto
    {
        public string UserId { get; set; } = string.Empty;
        public decimal TotalAEPSAmount { get; set; }
        public decimal TotalRazorpayAmount { get; set; }
        public decimal TotalMATMAmount { get; set; }
        public decimal AEPSWithdrawnAmount { get; set; }
        public decimal RazorpayWithdrawnAmount { get; set; }
        public decimal MATMWithdrawnAmount { get; set; }
        public decimal AvailableAEPSAmount { get; set; }
        public decimal AvailableRazorpayAmount { get; set; }
        public decimal AvailableMATMAmount { get; set; }
        public DateTime SettlementFromDate { get; set; }
        public DateTime SettlementToDate { get; set; }
        public List<UserSettlementDetail> UserSettlements { get; set; } = new();
    }

    public class UserSettlementDetail
    {
        public string UserId { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public decimal AEPSAmount { get; set; }
        public decimal RazorpayAmount { get; set; }
        public decimal MATMAmount { get; set; }
        public decimal AEPSWithdrawn { get; set; }
        public decimal RazorpayWithdrawn { get; set; }
        public decimal MATMWithdrawn { get; set; }
        public decimal AvailableAEPS { get; set; }
        public decimal AvailableRazorpay { get; set; }
        public decimal AvailableMATM { get; set; }
    }

    public class WithdrawalRequestDto
    {
        public string UserId { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string WithdrawalType { get; set; } = string.Empty; // "AEPS", "MATM", or "Razorpay"
        
        // Beneficiary details for payout
        public string BankName { get; set; } = string.Empty;
        public string BankAccount { get; set; } = string.Empty;
        public string Ifsc { get; set; } = string.Empty;
        public string BeneName { get; set; } = string.Empty;
        public string BeneEmail { get; set; } = string.Empty;
        public string BenePhone { get; set; } = string.Empty;
        public string BeneAddress { get; set; } = string.Empty;
        public string Latitude { get; set; } = "0";
        public string Longitude { get; set; } = "0";
        public string? ComingFrom { get; set; } = "Web";
    }

    public class WithdrawalResponseDto
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public decimal RemainingAmount { get; set; }
        public decimal NewWalletBalance { get; set; }
        public decimal Charge { get; set; }
        
        // Payout details
        public string? PayoutTransactionId { get; set; }
        public string? PayoutStatus { get; set; }
    }
}

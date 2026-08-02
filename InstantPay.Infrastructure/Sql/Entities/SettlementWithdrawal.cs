using System;

namespace InstantPay.Infrastructure.Sql.Entities;

public partial class SettlementWithdrawal
{
    public int Id { get; set; }

    public string UserName { get; set; } = null!;
    public string UserId { get; set; } = null!;

    public decimal Amount { get; set; }

    public decimal Charge { get; set; } // Charge for the withdrawal

    public string WithdrawalType { get; set; } = null!; // "AEPS" or "Razorpay"

    public DateTime WithdrawalDate { get; set; } = DateTime.UtcNow;

    public DateTime SettlementFromDate { get; set; }

    public DateTime SettlementToDate { get; set; }

    public string? Remarks { get; set; }

    // Beneficiary details
    public string? BankAccount { get; set; }
    public string? Ifsc { get; set; }
    public string? BeneName { get; set; }
    public string? BeneEmail { get; set; }
    public string? BenePhone { get; set; }
    public string? BeneAddress { get; set; }
    public string? Latitude { get; set; }
    public string? Longitude { get; set; }

    // Payout details
    public string? PayoutTransactionId { get; set; }
    public string? PayoutReferenceId { get; set; }
    public string? PayoutStatus { get; set; } // SUCCESS, FAILED, PENDING
    public string? PayoutResponse { get; set; } // Raw response from payout API
    public string? ApiRequest { get; set; } // Request payload sent to payout API
    public string? ApiMsg { get; set; } // Short API status message

    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public string? RRN { get; set; }
    public string? BankName { get; set; }
    public string? ComingFrom { get; set; }

}

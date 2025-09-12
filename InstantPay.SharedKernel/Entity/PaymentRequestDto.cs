using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InstantPay.SharedKernel.Entity
{
    // Application/DTOs/PaymentRequestDto.cs
    public class PaymentRequestDto
    {
        public Guid BankId { get; set; }
        public int UserId { get; set; }
        public decimal Amount { get; set; }
        public string? TxnId { get; set; }
        public string? DeposideMode { get; set; }
        public IFormFile? TxnSlip { get; set; }
    }

    // Application/DTOs/PaymentUpdateDto.cs
    public class PaymentUpdateDto
    {
        public Guid PaymentId { get; set; }
        public string? Status { get; set; }
        public string? AdminRemarks { get; set; }
        public int ModifiedBy { get; set; }
    }

    // Application/DTOs/PaymentResponseDto.cs
    public class PaymentResponseDto
    {
        public Guid PaymentId { get; set; }
        public string? TxnId { get; set; }
        public string? UserName { get; set; }
        public string? UserType { get; set; }
        public string? BankName { get; set; }
        public string? AccountNo { get; set; }
        public decimal Amount { get; set; }
        public string? DepositeMode { get; set; }
        public string? TxnSlipFileName { get; set; }
        public string? TxnSlipPath { get; set; }
        public string? Status { get; set; }
        public string? AdminRemarks { get; set; }
    }

}

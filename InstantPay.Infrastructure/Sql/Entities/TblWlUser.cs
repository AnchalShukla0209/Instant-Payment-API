using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace InstantPay.Infrastructure.Sql.Entities;

public partial class TblWlUsers
{
    public int Id { get; set; }

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

    public string? Aeps { get; set; }

    public string? BillPayment { get; set; }

    public string? MicroAtm { get; set; }

    public string? Apitransfer { get; set; }

    public string? Margin { get; set; }

    public string? Debit { get; set; }

    public string? Status { get; set; }

    public DateTime? RegDate { get; set; }

    public string? TxnPin { get; set; }

    public string? PlanId { get; set; }
    [NotMapped]
    public IFormFile PanCardFile { get; set; }
    [NotMapped]
    public IFormFile AadharCardFile { get; set; }
    [NotMapped]
    public IFormFile ProfileFile { get; set; }
    [NotMapped]
    public IFormFile OtherFile { get; set; }
}

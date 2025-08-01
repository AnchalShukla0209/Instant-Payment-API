using System;
using System.Collections.Generic;

namespace InstantPay.Infrastructure.Sql.Entities;

public partial class TblBankDetail
{
    public int Id { get; set; }

    public string? BankName { get; set; }

    public string? AccountNumber { get; set; }

    public string? Ifsccode { get; set; }

    public string? Phone { get; set; }

    public string? Accounttype { get; set; }

    public string? Address1 { get; set; }

    public string? Address2 { get; set; }

    public string? State { get; set; }

    public string? City { get; set; }

    public string? Zipcode { get; set; }

    public string? Status { get; set; }

    public decimal? Charge { get; set; }
}

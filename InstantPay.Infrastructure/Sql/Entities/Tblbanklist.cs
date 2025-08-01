using System;
using System.Collections.Generic;

namespace InstantPay.Infrastructure.Sql.Entities;

public partial class Tblbanklist
{
    public int Id { get; set; }

    public string? Bankname { get; set; }

    public string? Ifsc { get; set; }

    public string? BankId { get; set; }

    public string? Status { get; set; }

    public string? Picture { get; set; }
}

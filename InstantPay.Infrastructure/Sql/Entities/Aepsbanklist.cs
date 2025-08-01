using System;
using System.Collections.Generic;

namespace InstantPay.Infrastructure.Sql.Entities;

public partial class Aepsbanklist
{
    public int Id { get; set; }

    public string? BankName { get; set; }

    public string? Nbin { get; set; }

    public string? Status { get; set; }
}

using System;
using System.Collections.Generic;

namespace InstantPay.Infrastructure.Sql.Entities;

public partial class BeneReg
{
    public int Id { get; set; }

    public string SenderMobile { get; set; } = null!;

    public string? SenderId { get; set; }

    public string? BeneName { get; set; }

    public string? AccountNo { get; set; }

    public string? Ifsccode { get; set; }

    public string? AccountType { get; set; }

    public string? BankName { get; set; }

    public string? Status { get; set; }

    public string? Avstatus { get; set; }

    public DateTime? ReqDate { get; set; }

    public string? UserId { get; set; }
}

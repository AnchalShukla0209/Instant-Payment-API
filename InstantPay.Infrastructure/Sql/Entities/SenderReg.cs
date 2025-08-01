using System;
using System.Collections.Generic;

namespace InstantPay.Infrastructure.Sql.Entities;

public partial class SenderReg
{
    public int Id { get; set; }

    public string SenderName { get; set; } = null!;

    public string? SenderMobile { get; set; }

    public string? Address { get; set; }

    public string? Pincode { get; set; }

    public string? Status { get; set; }

    public DateTime? ReqDate { get; set; }

    public string? UserId { get; set; }

    public int? Limit { get; set; }
}

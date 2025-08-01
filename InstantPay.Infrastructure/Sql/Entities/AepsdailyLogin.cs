using System;
using System.Collections.Generic;

namespace InstantPay.Infrastructure.Sql.Entities;

public partial class AepsdailyLogin
{
    public int Id { get; set; }

    public string? UserId { get; set; }

    public string? LoginType { get; set; }

    public DateTime? Logindate { get; set; }
}

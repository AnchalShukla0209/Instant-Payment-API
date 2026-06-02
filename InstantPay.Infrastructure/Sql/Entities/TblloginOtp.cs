using System;
using System.Collections.Generic;

namespace InstantPay.Infrastructure.Sql.Entities;

public partial class TblloginOtp
{
    public int Id { get; set; }

    public bool? IsUsed { get; set; }

    public string? UserId { get; set; }



    public DateTime? CreatedAt { get; set; }

    public string? OTP { get; set; }

    public DateTime? ExpiresAt { get; set; }
}

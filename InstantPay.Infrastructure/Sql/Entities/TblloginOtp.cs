using System;
using System.Collections.Generic;

namespace InstantPay.Infrastructure.Sql.Entities;

public partial class TblloginOtp
{
    public int Id { get; set; }

    public string? Otptype { get; set; }

    public string? UserId { get; set; }

    public string? Ipaddress { get; set; }

    public DateTime? Reqdate { get; set; }
}

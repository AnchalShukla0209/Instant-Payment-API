using System;
using System.Collections.Generic;

namespace InstantPay.Infrastructure.Sql.Entities;

public partial class Tblloginlog
{
    public int Id { get; set; }

    public string UserId { get; set; } = null!;

    public string? Macaddress { get; set; }

    public string? Ipaddress { get; set; }

    public DateTime? LoginTime { get; set; }

    public string? Usertype { get; set; }
}

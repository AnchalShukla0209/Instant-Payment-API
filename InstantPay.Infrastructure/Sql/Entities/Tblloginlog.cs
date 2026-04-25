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

    public bool OTPVerified { get; set; } = false;

    public string? BrowserFingerprint { get; set; }

    public string? BrowserName { get; set; }

    public string? BrowserVersion { get; set; }

    public string? OperatingSystem { get; set; }

    public string? DeviceType { get; set; }

    public string? Country { get; set; }

    public string? Region { get; set; }

    public string? City { get; set; }

    public string? Latitude { get; set; }

    public string? Longitude { get; set; }

    public string? ISP { get; set; }
}

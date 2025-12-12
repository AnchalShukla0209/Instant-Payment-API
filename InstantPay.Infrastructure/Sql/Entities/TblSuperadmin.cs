using System;
using System.Collections.Generic;

namespace InstantPay.Infrastructure.Sql.Entities;

public partial class TblSuperadmin
{
    public int Id { get; set; }

    public string? Name { get; set; }

    public string? Mobileno { get; set; }

    public string? Username { get; set; }

    public string? Password { get; set; }

    public string? Status { get; set; }

    public string? TxnPin { get; set; }

    public string? Refundpin { get; set; }

    public string? Dmtapi { get; set; }
    public string? Mpin { get; set; }
}

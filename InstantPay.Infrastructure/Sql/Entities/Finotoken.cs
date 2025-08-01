using System;
using System.Collections.Generic;

namespace InstantPay.Infrastructure.Sql.Entities;

public partial class Finotoken
{
    public int Id { get; set; }

    public string? TokenKey { get; set; }

    public DateTime? Reqdate { get; set; }
}

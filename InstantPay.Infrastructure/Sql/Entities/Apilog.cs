using System;
using System.Collections.Generic;

namespace InstantPay.Infrastructure.Sql.Entities;

public partial class Apilog
{
    public int Id { get; set; }

    public string? Request { get; set; }

    public string? Response { get; set; }

    public string? Apiname { get; set; }

    public DateTime? Reqdatae { get; set; }
}

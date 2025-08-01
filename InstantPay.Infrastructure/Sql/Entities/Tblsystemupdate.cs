using System;
using System.Collections.Generic;

namespace InstantPay.Infrastructure.Sql.Entities;

public partial class Tblsystemupdate
{
    public int Id { get; set; }

    public string? Remark { get; set; }

    public DateTime? Lastupdate { get; set; }
}

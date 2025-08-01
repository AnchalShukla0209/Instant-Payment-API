using System;
using System.Collections.Generic;

namespace InstantPay.Infrastructure.Sql.Entities;

public partial class BbpsOperator
{
    public int Id { get; set; }

    public string? BillType { get; set; }

    public string? BillTypeId { get; set; }

    public string? OperatorName { get; set; }

    public string? Status { get; set; }

    public string? InputParameter { get; set; }

    public string? InputParameter2 { get; set; }

    public string? ProviderId { get; set; }

    public string? Picture { get; set; }
}

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace InstantPay.Infrastructure.Sql.Entities;

public partial class MasterProvider
{
    [Key]
    public int ProviderId { get; set; }
    public string? ProviderCode { get; set; }
    public string? ProviderName { get; set; }
    public bool? IsEnabled { get; set; }
}

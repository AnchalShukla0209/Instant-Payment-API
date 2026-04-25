using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace InstantPay.Infrastructure.Sql.Entities;

public partial class ServiceProvider
{
    [Key]
    public int Id { get; set; }
    public string? ServiceCode { get; set; }
    public string? ProviderCode { get; set; }
    public bool? IsEnabled { get; set; }
}

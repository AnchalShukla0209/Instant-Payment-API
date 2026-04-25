using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace InstantPay.Infrastructure.Sql.Entities;

public partial class ServiceProviderFeatureMap
{
    [Key]
    public int Id { get; set; }
    public string? ServiceCode { get; set; }
    public string? ProviderCode { get; set; }
    public string? FeatureCode { get; set; }
    public bool? IsEnabled { get; set; }
}

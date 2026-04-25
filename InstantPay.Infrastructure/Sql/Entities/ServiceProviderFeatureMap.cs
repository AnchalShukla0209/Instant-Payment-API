using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace InstantPay.Infrastructure.Sql.Entities;

public partial class MasterFeature
{
    [Key]
    public int FeatureId { get; set; }
    public string? ServiceCode { get; set; }
    public string? FeatureCode { get; set; }
    public string? FeatureName { get; set; }
    public string? Icon { get; set; }
    public string? ExtraConfig { get; set; }
    public bool? IsEnabled { get; set; }
    public int? DisplayOrder { get; set; }
}

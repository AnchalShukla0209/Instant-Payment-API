using System;
using System.ComponentModel.DataAnnotations;

namespace InstantPay.Infrastructure.Sql.Entities;

public partial class APICode
{
    public int Id { get; set; }

    public string APICodeValue { get; set; } = null!;

    public string Name { get; set; } = null!;

    public bool IsActive { get; set; } = true;

    public bool IsDeleted { get; set; } = false;

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public string? CreatedBy { get; set; }
}

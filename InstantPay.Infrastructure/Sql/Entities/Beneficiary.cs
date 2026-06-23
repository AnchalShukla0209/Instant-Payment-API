using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace InstantPay.Infrastructure.Sql.Entities;

public class Beneficiary
{
    [Key]
    public int Id { get; set; }

    public bool Status { get; set; }

    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [Column("account_number")]
    [Required]
    [MaxLength(50)]
    public string AccountNumber { get; set; } = string.Empty;

    [Column("bank_name")]
    [Required]
    [MaxLength(200)]
    public string BankName { get; set; } = string.Empty;

    [Column("ifsc")]
    [Required]
    [MaxLength(20)]
    public string Ifsc { get; set; } = string.Empty;

    [Column("customer_number")]
    [Required]
    [MaxLength(20)]
    public string CustomerNumber { get; set; } = string.Empty;

    [Column("createdOn")]
    public DateTime CreatedOn { get; set; }

    [Column("updatedOn")]
    public DateTime UpdatedOn { get; set; }
}

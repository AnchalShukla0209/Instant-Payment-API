using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace InstantPay.Infrastructure.Sql.Entities;

public class Sender
{
    [Key]
    public int Id { get; set; }

    [Column("sender_mobile")]
    [Required]
    [MaxLength(20)]
    public string SenderMobile { get; set; } = string.Empty;

    [Column("first_name")]
    [Required]
    [MaxLength(100)]
    public string FirstName { get; set; } = string.Empty;

    [Column("last_name")]
    [MaxLength(100)]
    public string LastName { get; set; } = string.Empty;

    [Column("address")]
    [MaxLength(500)]
    public string Address { get; set; } = string.Empty;

    [Column("pincode")]
    [MaxLength(10)]
    public string Pincode { get; set; } = string.Empty;

    [Column("state")]
    [MaxLength(100)]
    public string State { get; set; } = string.Empty;

    [Column("is_kyc_verified")]
    public bool IsKycVerified { get; set; }

    [Column("otp")]
    [MaxLength(10)]
    public string? Otp { get; set; }

    [Column("otp_expiry")]
    public DateTime? OtpExpiry { get; set; }

    [Column("created_on")]
    public DateTime CreatedOn { get; set; }

    [Column("updated_on")]
    public DateTime UpdatedOn { get; set; }
}

using Microsoft.EntityFrameworkCore;

namespace InstantPay.Infrastructure.Sql.Entities;

public class SenderDbContext : DbContext
{
    public SenderDbContext(DbContextOptions<SenderDbContext> options)
        : base(options)
    {
    }

    public DbSet<Sender> Senders { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Sender>(entity =>
        {
            entity.ToTable("Senders", "dbo");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.SenderMobile).HasMaxLength(20).IsRequired();
            entity.Property(e => e.FirstName).HasMaxLength(100).IsRequired();
            entity.Property(e => e.LastName).HasMaxLength(100);
            entity.Property(e => e.Address).HasMaxLength(500);
            entity.Property(e => e.Pincode).HasMaxLength(10);
            entity.Property(e => e.State).HasMaxLength(100);
            entity.Property(e => e.IsKycVerified).HasDefaultValue(false);
            entity.Property(e => e.Otp).HasMaxLength(10);
            entity.Property(e => e.OtpExpiry).HasColumnType("datetime");
            entity.Property(e => e.CreatedOn).HasColumnType("datetime").HasDefaultValueSql("GETUTCDATE()");
            entity.Property(e => e.UpdatedOn).HasColumnType("datetime").HasDefaultValueSql("GETUTCDATE()");

            entity.HasIndex(e => e.SenderMobile).IsUnique();
            entity.HasIndex(e => e.IsKycVerified);
        });
    }
}

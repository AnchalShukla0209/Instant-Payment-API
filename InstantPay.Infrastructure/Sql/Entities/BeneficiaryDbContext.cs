using Microsoft.EntityFrameworkCore;

namespace InstantPay.Infrastructure.Sql.Entities;

public class BeneficiaryDbContext : DbContext
{
    public BeneficiaryDbContext(DbContextOptions<BeneficiaryDbContext> options)
        : base(options)
    {
    }

    public DbSet<Beneficiary> Beneficiaries { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Beneficiary>(entity =>
        {
            entity.ToTable("Beneficiaries", "dbo");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).HasMaxLength(200).IsRequired();
            entity.Property(e => e.AccountNumber).HasMaxLength(50).IsRequired();
            entity.Property(e => e.BankName).HasMaxLength(200).IsRequired();
            entity.Property(e => e.Ifsc).HasMaxLength(20).IsRequired();
            entity.Property(e => e.CustomerNumber).HasMaxLength(20).IsRequired();
            entity.Property(e => e.CreatedOn).HasColumnType("datetime").HasDefaultValueSql("GETDATE()");
            entity.Property(e => e.UpdatedOn).HasColumnType("datetime").HasDefaultValueSql("GETDATE()");

            entity.HasIndex(e => e.AccountNumber);
            entity.HasIndex(e => e.Ifsc);
            entity.HasIndex(e => e.CustomerNumber);
        });
    }
}

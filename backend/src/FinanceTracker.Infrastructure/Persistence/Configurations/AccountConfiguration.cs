using FinanceTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceTracker.Infrastructure.Persistence.Configurations;

public sealed class AccountConfiguration : IEntityTypeConfiguration<Account>
{
    public void Configure(EntityTypeBuilder<Account> builder)
    {
        builder.ToTable("accounts");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(100).IsRequired();
        builder.Property(x => x.CurrencyCode).HasMaxLength(3).IsRequired();
        builder.Property(x => x.OpeningBalance).HasPrecision(18, 2).IsRequired();
        builder.Property(x => x.CurrentBalance).HasPrecision(18, 2).IsRequired();
        builder.Property(x => x.InstitutionName).HasMaxLength(120);
        builder.Property(x => x.Last4Digits).HasMaxLength(4);
        builder.HasIndex(x => new { x.UserId, x.IsArchived });
        builder.HasIndex(x => new { x.UserId, x.Name });
    }
}

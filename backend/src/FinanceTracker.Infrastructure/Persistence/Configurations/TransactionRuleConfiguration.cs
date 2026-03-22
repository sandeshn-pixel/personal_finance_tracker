using FinanceTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceTracker.Infrastructure.Persistence.Configurations;

public sealed class TransactionRuleConfiguration : IEntityTypeConfiguration<TransactionRule>
{
    public void Configure(EntityTypeBuilder<TransactionRule> builder)
    {
        builder.ToTable("transaction_rules");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name)
            .HasMaxLength(80)
            .IsRequired();

        builder.Property(x => x.ConditionJson)
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(x => x.ActionJson)
            .HasColumnType("jsonb")
            .IsRequired();

        builder.HasIndex(x => new { x.UserId, x.IsActive, x.Priority });

        builder.HasOne(x => x.User)
            .WithMany(x => x.TransactionRules)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

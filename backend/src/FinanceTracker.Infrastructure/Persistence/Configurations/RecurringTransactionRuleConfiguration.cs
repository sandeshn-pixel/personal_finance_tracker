using FinanceTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceTracker.Infrastructure.Persistence.Configurations;

public sealed class RecurringTransactionRuleConfiguration : IEntityTypeConfiguration<RecurringTransactionRule>
{
    public void Configure(EntityTypeBuilder<RecurringTransactionRule> builder)
    {
        builder.ToTable("recurring_transaction_rules");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Title)
            .HasMaxLength(120)
            .IsRequired();

        builder.Property(x => x.Amount)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.HasIndex(x => new { x.UserId, x.Status, x.NextRunDateUtc });
        builder.HasIndex(x => new { x.UserId, x.AccountId });
        builder.HasIndex(x => new { x.UserId, x.CategoryId });

        builder.HasOne(x => x.User)
            .WithMany(x => x.RecurringTransactionRules)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Category)
            .WithMany()
            .HasForeignKey(x => x.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Account)
            .WithMany(x => x.RecurringTransactionRules)
            .HasForeignKey(x => x.AccountId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.TransferAccount)
            .WithMany(x => x.RecurringTransferTransactionRules)
            .HasForeignKey(x => x.TransferAccountId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
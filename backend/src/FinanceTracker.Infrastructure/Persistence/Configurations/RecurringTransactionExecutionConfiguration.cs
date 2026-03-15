using FinanceTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceTracker.Infrastructure.Persistence.Configurations;

public sealed class RecurringTransactionExecutionConfiguration : IEntityTypeConfiguration<RecurringTransactionExecution>
{
    public void Configure(EntityTypeBuilder<RecurringTransactionExecution> builder)
    {
        builder.ToTable("recurring_transaction_executions");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.FailureReason)
            .HasMaxLength(280);

        builder.HasIndex(x => new { x.RecurringTransactionRuleId, x.ScheduledForDateUtc }).IsUnique();
        builder.HasIndex(x => new { x.Status, x.ProcessedAtUtc });

        builder.HasOne(x => x.RecurringTransactionRule)
            .WithMany(x => x.Executions)
            .HasForeignKey(x => x.RecurringTransactionRuleId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Transaction)
            .WithMany()
            .HasForeignKey(x => x.TransactionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
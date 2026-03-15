using FinanceTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceTracker.Infrastructure.Persistence.Configurations;

public sealed class GoalEntryConfiguration : IEntityTypeConfiguration<GoalEntry>
{
    public void Configure(EntityTypeBuilder<GoalEntry> builder)
    {
        builder.ToTable("goal_entries");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Amount)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(x => x.GoalAmountAfterEntry)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(x => x.Note)
            .HasMaxLength(240);

        builder.HasIndex(x => new { x.GoalId, x.OccurredAtUtc });
        builder.HasIndex(x => new { x.UserId, x.OccurredAtUtc });

        builder.HasOne(x => x.Goal)
            .WithMany(x => x.Entries)
            .HasForeignKey(x => x.GoalId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.User)
            .WithMany(x => x.GoalEntries)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Account)
            .WithMany(x => x.GoalEntries)
            .HasForeignKey(x => x.AccountId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
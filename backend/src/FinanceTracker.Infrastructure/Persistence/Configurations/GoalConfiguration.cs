using FinanceTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceTracker.Infrastructure.Persistence.Configurations;

public sealed class GoalConfiguration : IEntityTypeConfiguration<Goal>
{
    public void Configure(EntityTypeBuilder<Goal> builder)
    {
        builder.ToTable("goals");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name)
            .HasMaxLength(120)
            .IsRequired();

        builder.Property(x => x.TargetAmount)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(x => x.CurrentAmount)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(x => x.Icon)
            .HasMaxLength(64);

        builder.Property(x => x.Color)
            .HasMaxLength(32);

        builder.HasIndex(x => new { x.UserId, x.Status, x.TargetDateUtc });
        builder.HasIndex(x => new { x.UserId, x.LinkedAccountId });

        builder.HasOne(x => x.User)
            .WithMany(x => x.Goals)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.LinkedAccount)
            .WithMany(x => x.Goals)
            .HasForeignKey(x => x.LinkedAccountId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
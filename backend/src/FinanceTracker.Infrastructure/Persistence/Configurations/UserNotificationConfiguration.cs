using FinanceTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceTracker.Infrastructure.Persistence.Configurations;

public sealed class UserNotificationConfiguration : IEntityTypeConfiguration<UserNotification>
{
    public void Configure(EntityTypeBuilder<UserNotification> builder)
    {
        builder.ToTable("user_notifications");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Title)
            .HasMaxLength(120)
            .IsRequired();

        builder.Property(x => x.Message)
            .HasMaxLength(320)
            .IsRequired();

        builder.Property(x => x.Route)
            .HasMaxLength(180);

        builder.Property(x => x.DeduplicationKey)
            .HasMaxLength(180);

        builder.HasIndex(x => new { x.UserId, x.ReadAtUtc, x.CreatedUtc });
        builder.HasIndex(x => new { x.UserId, x.DeduplicationKey }).IsUnique().HasFilter("\"DeduplicationKey\" IS NOT NULL");

        builder.HasOne(x => x.User)
            .WithMany(x => x.Notifications)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
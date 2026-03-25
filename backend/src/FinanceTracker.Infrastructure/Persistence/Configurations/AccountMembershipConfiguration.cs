using FinanceTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceTracker.Infrastructure.Persistence.Configurations;

public sealed class AccountMembershipConfiguration : IEntityTypeConfiguration<AccountMembership>
{
    public void Configure(EntityTypeBuilder<AccountMembership> builder)
    {
        builder.ToTable("account_memberships");
        builder.HasKey(x => x.Id);

        builder.HasOne(x => x.Account)
            .WithMany(x => x.Memberships)
            .HasForeignKey(x => x.AccountId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.User)
            .WithMany(x => x.AccountMemberships)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.InvitedByUser)
            .WithMany(x => x.InvitedAccountMemberships)
            .HasForeignKey(x => x.InvitedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.LastModifiedByUser)
            .WithMany(x => x.ModifiedAccountMemberships)
            .HasForeignKey(x => x.LastModifiedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.AccountId, x.UserId }).IsUnique();
        builder.HasIndex(x => x.UserId);
    }
}

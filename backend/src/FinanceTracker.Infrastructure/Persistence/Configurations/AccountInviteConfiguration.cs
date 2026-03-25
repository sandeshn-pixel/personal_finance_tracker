using FinanceTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceTracker.Infrastructure.Persistence.Configurations;

public sealed class AccountInviteConfiguration : IEntityTypeConfiguration<AccountInvite>
{
    public void Configure(EntityTypeBuilder<AccountInvite> builder)
    {
        builder.ToTable("account_invites");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Email)
            .IsRequired()
            .HasMaxLength(320);

        builder.Property(x => x.TokenHash)
            .IsRequired()
            .HasMaxLength(128);

        builder.HasOne(x => x.Account)
            .WithMany(x => x.Invites)
            .HasForeignKey(x => x.AccountId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.InvitedByUser)
            .WithMany(x => x.SentAccountInvites)
            .HasForeignKey(x => x.InvitedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.AcceptedByUser)
            .WithMany(x => x.AcceptedAccountInvites)
            .HasForeignKey(x => x.AcceptedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.RevokedByUser)
            .WithMany(x => x.RevokedAccountInvites)
            .HasForeignKey(x => x.RevokedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.TokenHash).IsUnique();
        builder.HasIndex(x => new { x.AccountId, x.Email, x.Status });
        builder.HasIndex(x => new { x.Email, x.Status, x.ExpiresUtc });
    }
}

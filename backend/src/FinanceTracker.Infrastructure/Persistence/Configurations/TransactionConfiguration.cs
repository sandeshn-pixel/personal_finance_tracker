using FinanceTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceTracker.Infrastructure.Persistence.Configurations;

public sealed class TransactionConfiguration : IEntityTypeConfiguration<Transaction>
{
    public void Configure(EntityTypeBuilder<Transaction> builder)
    {
        builder.ToTable("transactions");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Amount).HasPrecision(18, 2).IsRequired();
        builder.Property(x => x.Note).HasMaxLength(500);
        builder.Property(x => x.Merchant).HasMaxLength(120);
        builder.Property(x => x.PaymentMethod).HasMaxLength(50);

        builder.HasOne(x => x.Account)
            .WithMany(x => x.Transactions)
            .HasForeignKey(x => x.AccountId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.TransferAccount)
            .WithMany(x => x.TransferTransactions)
            .HasForeignKey(x => x.TransferAccountId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Category)
            .WithMany(x => x.Transactions)
            .HasForeignKey(x => x.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.UserId, x.DateUtc });
        builder.HasIndex(x => new { x.UserId, x.AccountId, x.DateUtc });
        builder.HasIndex(x => new { x.UserId, x.CategoryId, x.DateUtc });
        builder.HasIndex(x => new { x.UserId, x.Type, x.DateUtc });
    }
}

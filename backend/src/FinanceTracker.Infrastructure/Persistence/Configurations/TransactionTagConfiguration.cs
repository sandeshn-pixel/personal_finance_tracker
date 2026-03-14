using FinanceTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceTracker.Infrastructure.Persistence.Configurations;

public sealed class TransactionTagConfiguration : IEntityTypeConfiguration<TransactionTag>
{
    public void Configure(EntityTypeBuilder<TransactionTag> builder)
    {
        builder.ToTable("transaction_tags");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Value).HasMaxLength(40).IsRequired();
        builder.HasIndex(x => new { x.TransactionId, x.Value }).IsUnique();
        builder.HasOne(x => x.Transaction)
            .WithMany(x => x.Tags)
            .HasForeignKey(x => x.TransactionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

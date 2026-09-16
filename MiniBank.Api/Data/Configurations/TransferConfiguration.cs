using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MiniBank.Api.Entities;

namespace MiniBank.Api.Data.Configurations;

public sealed class TransferConfiguration : IEntityTypeConfiguration<Transfer>
{
    public void Configure(EntityTypeBuilder<Transfer> builder)
    {
        builder.HasKey(transfer => transfer.Id);
        builder.Property(transfer => transfer.Amount).HasPrecision(18, 2);
        builder.Property(transfer => transfer.Description).HasMaxLength(255).IsRequired();
        builder.Property(transfer => transfer.TransactionDate).HasUtcConversion();
        builder.Property(transfer => transfer.CreatedAt).HasUtcConversion();
        builder
            .HasOne(transfer => transfer.FromAccount)
            .WithMany()
            .HasForeignKey(transfer => transfer.FromAccountId)
            .OnDelete(DeleteBehavior.Restrict);
        builder
            .HasOne(transfer => transfer.ToAccount)
            .WithMany()
            .HasForeignKey(transfer => transfer.ToAccountId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

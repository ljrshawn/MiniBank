using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MiniBank.Api.Entities;

namespace MiniBank.Api.Data.Configurations;

public sealed class AccountConfiguration : IEntityTypeConfiguration<Account>
{
    public void Configure(EntityTypeBuilder<Account> builder)
    {
        builder.HasKey(account => account.Id);
        builder.Property(account => account.AccountNumber).HasMaxLength(34).IsRequired();
        builder.HasIndex(account => account.AccountNumber).IsUnique();
        builder.Property(account => account.Balance).HasPrecision(18, 2);
        builder.Property(account => account.Version).IsConcurrencyToken().ValueGeneratedNever();
        builder.Property(account => account.CreatedAt).HasUtcConversion();
        builder
            .HasOne(account => account.Customer)
            .WithMany(customer => customer.Accounts)
            .HasForeignKey(account => account.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

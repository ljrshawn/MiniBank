using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MiniBank.Api.Entities;

namespace MiniBank.Api.Data.Configurations;

public sealed class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.HasKey(customer => customer.Id);
        builder.Property(customer => customer.FirstName).HasMaxLength(50).IsRequired();
        builder.Property(customer => customer.LastName).HasMaxLength(100).IsRequired();
        builder
            .Property(customer => customer.Email)
            .HasMaxLength(254)
            .UseCollation(DatabaseCollations.Email)
            .IsRequired();
        builder.HasIndex(customer => customer.Email).IsUnique();
        builder.Property(customer => customer.TaxFileNumber).HasMaxLength(10);
        builder.Property(customer => customer.PhoneNumber).HasMaxLength(30).IsRequired();
        builder.Property(customer => customer.CreatedAt).HasUtcConversion();
        builder
            .HasOne(customer => customer.User)
            .WithOne(user => user.Customer)
            .HasForeignKey<Customer>(customer => customer.UserId);
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MiniBank.Api.Entities;

namespace MiniBank.Api.Data.Configurations;

public sealed class ApplicationUserConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> builder)
    {
        builder.ToTable("ApplicationUsers");
        builder.Property(user => user.UserName).UseCollation(DatabaseCollations.UserName);
        builder.HasIndex(user => user.NormalizedEmail).HasDatabaseName("EmailIndex").IsUnique();
        builder.Property(user => user.CreatedAt).HasUtcConversion();
    }
}

using Microsoft.EntityFrameworkCore;
using MiniBank.Api.Entities;

namespace MiniBank.Api.Data;

// A separate context keeps PostgreSQL migrations separate from the SQLite history.
public sealed class PostgresAppDbContext(DbContextOptions<PostgresAppDbContext> options)
    : AppDbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Account>().Property(account => account.Version)
            .IsRowVersion()
            .HasColumnName("xmin");

        modelBuilder.HasCollation(
            "minibank_email_nocase",
            locale: "und-u-ks-level2",
            provider: "icu",
            deterministic: false
        );
        modelBuilder.Entity<Customer>().Property(customer => customer.Email)
            .UseCollation("minibank_email_nocase");
    }
}

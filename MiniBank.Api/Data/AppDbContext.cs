using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using MiniBank.Api.Entities;

namespace MiniBank.Api.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options)
    : IdentityUserContext<ApplicationUser>(options)
{
    public DbSet<Customer> Customers => Set<Customer>();

    public DbSet<Account> Accounts => Set<Account>();

    public DbSet<BankTransaction> BankTransactions => Set<BankTransaction>();

    public DbSet<Transfer> Transfers => Set<Transfer>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.HasCollation(
            DatabaseCollations.Email,
            locale: "und-u-ks-level2",
            provider: "icu",
            deterministic: false
        );
        modelBuilder.HasCollation(
            DatabaseCollations.UserName,
            locale: "und-u-ks-level2",
            provider: "icu",
            deterministic: false
        );

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}

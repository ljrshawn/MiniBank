using Microsoft.EntityFrameworkCore;
using MiniBank.Api.Entities;

namespace MiniBank.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    protected AppDbContext(DbContextOptions options) : base(options) { }

    public DbSet<Customer> Customers => Set<Customer>();

    public DbSet<Account> Accounts => Set<Account>();

    public DbSet<BankTransaction> BankTransactions => Set<BankTransaction>();

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        UpdateAccountVersions();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default
    )
    {
        UpdateAccountVersions();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }

    private void UpdateAccountVersions()
    {
        if (!Database.IsSqlite())
        {
            return;
        }

        foreach (var entry in ChangeTracker.Entries<Account>())
        {
            var version = entry.Property(account => account.Version);
            if (entry.State == EntityState.Added)
            {
                version.CurrentValue = 1;
            }
            else if (entry.State == EntityState.Modified)
            {
                // Use the original value so retrying a failed save does not increment twice.
                version.CurrentValue = checked(version.OriginalValue + 1);
            }
        }
    }
}

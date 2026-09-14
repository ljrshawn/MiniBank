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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}

using Microsoft.EntityFrameworkCore;
using MiniBank.Api.Entities;

namespace MiniBank.Api.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Customer> Customers => Set<Customer>();

    public DbSet<Account> Accounts => Set<Account>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}

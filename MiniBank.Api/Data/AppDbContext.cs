using Microsoft.EntityFrameworkCore;
using MiniBank.Api.Entities;

namespace MiniBank.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Customer> Customers => Set<Customer>();

    public DbSet<Account> Accounts => Set<Account>();
}

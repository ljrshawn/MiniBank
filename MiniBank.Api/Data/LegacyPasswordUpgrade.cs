using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MiniBank.Api.Entities;

namespace MiniBank.Api.Data;

// Runs under EF's migration lock. The migration marks old plaintext explicitly;
// new Identity hashes are base64 and can never have this prefix.
internal static class LegacyPasswordUpgrade
{
    private const string LegacyPrefix = "legacy:";
    private const int BatchSize = 100;

    public static void Run(DbContext context)
    {
        if (
            !context
                .Database.GetAppliedMigrations()
                .Any(id => id.EndsWith("_HardenCustomerStorage", StringComparison.Ordinal))
        )
        {
            return;
        }

        List<Customer> customers;
        while ((customers = LegacyCustomers(context).ToList()).Count > 0)
        {
            HashPasswords(customers);
            context.SaveChanges();
            DetachCustomers(context, customers);
        }
    }

    public static async Task RunAsync(DbContext context, CancellationToken cancellationToken)
    {
        var migrations = await context.Database.GetAppliedMigrationsAsync(cancellationToken);
        if (!migrations.Any(id => id.EndsWith("_HardenCustomerStorage", StringComparison.Ordinal)))
        {
            return;
        }

        List<Customer> customers;
        while (
            (customers = await LegacyCustomers(context).ToListAsync(cancellationToken)).Count > 0
        )
        {
            HashPasswords(customers);
            await context.SaveChangesAsync(cancellationToken);
            DetachCustomers(context, customers);
        }
    }

    private static IQueryable<Customer> LegacyCustomers(DbContext context) =>
        context
            .Set<Customer>()
            .Where(customer => customer.PasswordHash.StartsWith(LegacyPrefix))
            .Take(BatchSize);

    private static void HashPasswords(IEnumerable<Customer> customers)
    {
        var hasher = new PasswordHasher<Customer>();
        foreach (var customer in customers)
        {
            customer.PasswordHash = hasher.HashPassword(
                customer,
                customer.PasswordHash[LegacyPrefix.Length..]
            );
        }
    }

    private static void DetachCustomers(DbContext context, IEnumerable<Customer> customers)
    {
        foreach (var customer in customers)
        {
            context.Entry(customer).State = EntityState.Detached;
        }
    }
}

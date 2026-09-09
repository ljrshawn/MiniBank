using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MiniBank.Api.Data;
using MiniBank.Api.Entities;

namespace MiniBank.Api.Tests;

public sealed class DatabaseMigrationTests : IAsyncLifetime
{
    private const string InitialMigration = "20260905015118_InitialCreate";
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(),
        "minibank-migrations",
        Guid.NewGuid().ToString("N")
    );
    private ServiceProvider _services = null!;

    public Task InitializeAsync()
    {
        Directory.CreateDirectory(_directory);
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["ConnectionStrings:DbConnection"] =
                        $"Data Source={Path.Combine(_directory, "test.db")};Pooling=False;Foreign Keys=True",
                }
            )
            .Build();

        _services = new ServiceCollection()
            .AddLogging()
            .AddAppDb(configuration)
            .BuildServiceProvider();
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        await _services.DisposeAsync();
        Directory.Delete(_directory, recursive: true);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Upgrade_preserves_data_and_hashes_legacy_passwords_once(
        bool useSynchronousMigration
    )
    {
        await using var scope = _services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await database.GetService<IMigrator>().MigrateAsync(InitialMigration);

        var customerId = Guid.NewGuid();
        const string password = "legacy:My original password";
        await InsertLegacyCustomerAsync(database, customerId, " David@example.com ", password);
        await database.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO "Accounts"
                ("Id", "CustomerId", "AccountNumber", "Balance", "AccountType", "AccountStatus", "CreatedAt")
            VALUES ({Guid.NewGuid()}, {customerId}, {"1000000001"}, {123.45m}, {1}, {0}, {DateTime.UtcNow})
            """
        );

        if (useSynchronousMigration)
        {
            database.Database.Migrate();
        }
        else
        {
            await database.Database.MigrateAsync();
        }

        var customer = await database.Customers.AsNoTracking().SingleAsync();
        Assert.Equal(customerId, customer.Id);
        Assert.Equal("David@example.com", customer.Email);
        Assert.Equal("123456789", customer.TaxFileNumber);
        Assert.Equal(DateTimeKind.Utc, customer.CreatedAt.Kind);
        Assert.Equal(123.45m, (await database.Accounts.SingleAsync()).Balance);
        Assert.Empty(await database.BankTransactions.ToListAsync());
        Assert.Equal(
            PasswordVerificationResult.Success,
            new PasswordHasher<Customer>().VerifyHashedPassword(
                customer,
                customer.PasswordHash,
                password
            )
        );

        var hash = customer.PasswordHash;
        await database.Database.MigrateAsync();
        Assert.Equal(hash, (await database.Customers.AsNoTracking().SingleAsync()).PasswordHash);
        Assert.False(database.Database.HasPendingModelChanges());
    }

    [Fact]
    public async Task Upgrade_rejects_legacy_duplicate_emails_without_losing_customers()
    {
        await using var scope = _services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await database.GetService<IMigrator>().MigrateAsync(InitialMigration);
        await InsertLegacyCustomerAsync(
            database,
            Guid.NewGuid(),
            " David@example.com ",
            "first password"
        );
        await InsertLegacyCustomerAsync(
            database,
            Guid.NewGuid(),
            "DAVID@example.com",
            "second password"
        );

        await Assert.ThrowsAsync<SqliteException>(() => database.Database.MigrateAsync());

        var count = await database
            .Database.SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM Customers")
            .SingleAsync();
        Assert.Equal(2, count);
        var passwords = await database
            .Database.SqlQueryRaw<string>("SELECT PassWord AS Value FROM Customers")
            .ToListAsync();
        Assert.Contains("first password", passwords);
        Assert.Contains("second password", passwords);
        Assert.Equal([InitialMigration], await database.Database.GetAppliedMigrationsAsync());
    }

    [Fact]
    public async Task Database_enforces_email_uniqueness_even_without_the_customer_service()
    {
        await using var scope = _services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await database.Database.MigrateAsync();

        database.Customers.Add(NewCustomer("David@example.com"));
        await database.SaveChangesAsync();
        database.Customers.Add(NewCustomer("DAVID@example.com"));

        var exception = await Assert.ThrowsAsync<DbUpdateException>(() =>
            database.SaveChangesAsync()
        );
        Assert.Equal(
            2067,
            Assert.IsType<SqliteException>(exception.InnerException).SqliteExtendedErrorCode
        );
    }

    private static Customer NewCustomer(string email) =>
        new()
        {
            Id = Guid.NewGuid(),
            FirstName = "David",
            LastName = "Smith",
            Email = email,
            PasswordHash = "test-only-placeholder",
            PhoneNumber = "+61 412 345 678",
            CreatedAt = DateTime.UtcNow,
        };

    private static Task InsertLegacyCustomerAsync(
        AppDbContext database,
        Guid id,
        string email,
        string password
    ) =>
        database.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO "Customers"
                ("Id", "FirstName", "LastName", "Email", "PassWord", "TaxFileNumber", "PhoneNumber", "CustomerStatus", "CreatedAt")
            VALUES ({id}, {"David"}, {"Smith"}, {email}, {password}, {"123456789"}, {"+61 412 345 678"}, {0}, {DateTime.UtcNow})
            """
        );
}

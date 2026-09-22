using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MiniBank.Api.Data;
using MiniBank.Api.Entities;
using MiniBank.Api.Enums;
using Npgsql;

namespace MiniBank.Api.Tests;

public sealed class DatabaseMigrationTests : IAsyncLifetime
{
    private const string InitialMigration = "20260922101849_InitialCreate";
    private readonly PostgresTestDatabase _database = new();

    public Task InitializeAsync()
    {
        _database.Create();
        return Task.CompletedTask;
    }

    public async Task DisposeAsync() => await _database.DisposeAsync();

    [Fact]
    public async Task Identity_migration_preserves_existing_users_customers_and_accounts()
    {
        await using var database = CreateContext();
        await database.GetService<IMigrator>().MigrateAsync(InitialMigration);
        var userId = Guid.NewGuid().ToString();
        var customerId = Guid.NewGuid();
        var timestamp = DateTime.UtcNow;
        const string passwordHash = "existing-password-hash";
        await database.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO "ApplicationUsers"
                ("Id", "UserName", "NormalizedUserName", "Email", "NormalizedEmail", "PasswordHash",
                 "CreatedAt", "EmailConfirmed", "PhoneNumberConfirmed", "TwoFactorEnabled", "LockoutEnabled", "AccessFailedCount")
            VALUES ({userId}, {"david@example.com"}, {"DAVID@EXAMPLE.COM"}, {"david@example.com"},
                    {"DAVID@EXAMPLE.COM"}, {passwordHash}, {timestamp}, false, false, false, false, 0)
            """
        );
        await database.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO "Customers"
                ("Id", "FirstName", "LastName", "Email", "PhoneNumber", "CustomerStatus", "CreatedAt", "UserId")
            VALUES ({customerId}, {"David"}, {"Smith"}, {"david@example.com"},
                    {"+61 412 345 678"}, 0, {timestamp}, {userId})
            """
        );
        await database.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO "Accounts"
                ("Id", "CustomerId", "AccountNumber", "Balance", "AccountType", "AccountStatus", "CreatedAt")
            VALUES ({Guid.NewGuid()}, {customerId}, {"100000001"}, {123.45m}, 1, 0, {timestamp})
            """
        );

        await database.Database.MigrateAsync();

        var customer = await database.Customers.Include(customer => customer.User).SingleAsync();
        Assert.Equal(customerId, customer.Id);
        Assert.Equal(userId, customer.User.Id);
        Assert.Equal(passwordHash, customer.User.PasswordHash);
        Assert.Equal(DateTimeKind.Utc, customer.CreatedAt.Kind);
        Assert.Equal(123.45m, (await database.Accounts.SingleAsync()).Balance);
        Assert.Empty(await database.Set<IdentityUserClaim<string>>().ToListAsync());
        Assert.False(database.Database.HasPendingModelChanges());
        await database.Database.MigrateAsync();
        Assert.Equal(1, await database.Users.CountAsync());
    }

    [Fact]
    public async Task Database_enforces_case_insensitive_customer_email_uniqueness()
    {
        await using var database = CreateContext();
        await database.Database.MigrateAsync();
        database.Customers.Add(NewCustomer("David@example.com"));
        await database.SaveChangesAsync();
        database.Customers.Add(NewCustomer("DAVID@example.com"));

        var exception = await Assert.ThrowsAsync<DbUpdateException>(() => database.SaveChangesAsync());
        var postgres = Assert.IsType<PostgresException>(exception.InnerException);
        Assert.Equal(PostgresErrorCodes.UniqueViolation, postgres.SqlState);
        Assert.Equal("IX_Customers_Email", postgres.ConstraintName);
        database.ChangeTracker.Clear();
        Assert.Equal(1, await database.Customers.CountAsync());
        Assert.Equal(1, await database.Users.CountAsync());
    }

    [Fact]
    public async Task Database_enforces_normalized_identity_email_uniqueness()
    {
        await using var database = CreateContext();
        await database.Database.MigrateAsync();
        database.Users.Add(new ApplicationUser { NormalizedEmail = "DAVID@EXAMPLE.COM" });
        await database.SaveChangesAsync();
        database.Users.Add(new ApplicationUser { NormalizedEmail = "DAVID@EXAMPLE.COM" });

        var exception = await Assert.ThrowsAsync<DbUpdateException>(() => database.SaveChangesAsync());
        var postgres = Assert.IsType<PostgresException>(exception.InnerException);
        Assert.Equal(PostgresErrorCodes.UniqueViolation, postgres.SqlState);
        Assert.Equal("EmailIndex", postgres.ConstraintName);
    }

    [Fact]
    public async Task Database_rejects_balances_outside_numeric_column_precision()
    {
        await using var database = CreateContext();
        await database.Database.MigrateAsync();
        var customer = NewCustomer("david@example.com");
        database.Accounts.Add(new Account
        {
            Customer = customer,
            AccountNumber = "123456789",
            AccountType = AccountType.Checking,
            Balance = decimal.MaxValue,
            CreatedAt = DateTime.UtcNow,
        });

        var exception = await Assert.ThrowsAsync<DbUpdateException>(() => database.SaveChangesAsync());
        Assert.Equal(PostgresErrorCodes.NumericValueOutOfRange,
            Assert.IsType<PostgresException>(exception.InnerException).SqlState);
    }

    private AppDbContext CreateContext() => new(
        new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(_database.ConnectionString).Options
    );

    private static Customer NewCustomer(string email) => new()
    {
        Id = Guid.NewGuid(),
        FirstName = "David",
        LastName = "Smith",
        Email = email,
        PhoneNumber = "+61 412 345 678",
        CreatedAt = DateTime.UtcNow,
        User = new ApplicationUser { CreatedAt = DateTime.UtcNow },
    };
}

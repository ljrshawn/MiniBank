using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using MiniBank.Api.Data;
using MiniBank.Api.Entities;
using MiniBank.Api.Enums;
using MiniBank.Api.Features.Accounts;

namespace MiniBank.Api.Tests;

public sealed class AccountNumberCollisionTests : IAsyncLifetime
{
    private const string ExistingAccountNumber = "123456789";
    private readonly MiniBankApiFactory _factory = new();
    private readonly Guid _customerId = Guid.NewGuid();
    private readonly Guid _existingAccountId = Guid.NewGuid();

    public async Task InitializeAsync()
    {
        await _factory.InitializeDatabaseAsync();
        await _factory.InDatabaseAsync(async database =>
        {
            database.Customers.Add(
                new Customer
                {
                    Id = _customerId,
                    FirstName = "Test",
                    LastName = "Customer",
                    Email = "collision-tests@example.com",
                    PasswordHash = "test-only-placeholder",
                    PhoneNumber = "+61 412 345 678",
                    CreatedAt = DateTime.UtcNow,
                }
            );
            database.Accounts.Add(
                new Account
                {
                    Id = _existingAccountId,
                    CustomerId = _customerId,
                    AccountNumber = ExistingAccountNumber,
                    AccountType = AccountType.Checking,
                    Balance = 25m,
                    CreatedAt = DateTime.UtcNow,
                }
            );
            await database.SaveChangesAsync();
        });
    }

    public async Task DisposeAsync() => await _factory.DisposeAsync();

    [Theory]
    [InlineData(1)]
    [InlineData(4)]
    public async Task Create_retries_number_collisions_and_saves_exactly_one_new_account(
        int collisions
    )
    {
        var interceptor = new AccountSaveInterceptor(collisions);
        await using var factory = WithInterceptor(interceptor);
        using var client = CreateClient(factory);

        using var response = await client.PostAsJsonAsync(
            $"/api/accounts/customers/{_customerId}",
            new CreateAccountRequest { AccountType = AccountType.Savings }
        );

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(collisions + 1, interceptor.Attempts);
        var account = await response.Content.ReadFromJsonAsync<AccountResponse>();
        Assert.NotNull(account);
        Assert.Matches("^[1-9][0-9]{8}$", account.AccountNumber);
        Assert.NotEqual(ExistingAccountNumber, account.AccountNumber);
        Assert.Equal(
            account,
            await client.GetFromJsonAsync<AccountResponse>(response.Headers.Location)
        );
        await _factory.InDatabaseAsync(async database =>
        {
            Assert.Equal(2, await database.Accounts.CountAsync());
            var stored = await database.Accounts.SingleAsync(item => item.Id == account.Id);
            Assert.Equal(account.AccountNumber, stored.AccountNumber);
            Assert.Equal(_customerId, stored.CustomerId);
            Assert.Equal(0m, stored.Balance);
            Assert.Equal(
                25m,
                (await database.Accounts.SingleAsync(item => item.Id == _existingAccountId)).Balance
            );
        });
    }

    [Fact]
    public async Task Create_stops_after_five_collisions_and_returns_service_unavailable()
    {
        var interceptor = new AccountSaveInterceptor(int.MaxValue);
        await using var factory = WithInterceptor(interceptor);
        using var client = CreateClient(factory);

        using var response = await client.PostAsJsonAsync(
            $"/api/accounts/customers/{_customerId}",
            new CreateAccountRequest { AccountType = AccountType.Savings }
        );

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal(5, interceptor.Attempts);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<JsonObject>();
        Assert.Equal(503, problem!["status"]!.GetValue<int>());
        Assert.NotNull(problem["traceId"]);
        Assert.DoesNotContain("Sqlite", problem.ToJsonString(), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(EntityState.Detached, interceptor.LastEntry!.State);
        await AssertOnlyOriginalAccountAsync();
    }

    [Fact]
    public async Task Create_does_not_retry_a_different_database_constraint_failure()
    {
        var interceptor = new AccountSaveInterceptor(0, conflictingId: _existingAccountId);
        await using var factory = WithInterceptor(interceptor);
        using var client = CreateClient(factory);

        using var response = await client.PostAsJsonAsync(
            $"/api/accounts/customers/{_customerId}",
            new CreateAccountRequest { AccountType = AccountType.Savings }
        );

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal(1, interceptor.Attempts);
        await AssertOnlyOriginalAccountAsync();
    }

    [Fact]
    public async Task Cancellation_after_a_collision_prevents_further_save_attempts()
    {
        using var cancellation = new CancellationTokenSource();
        var interceptor = new AccountSaveInterceptor(int.MaxValue, cancelOnFailure: cancellation);
        await _factory.InDatabaseAsync(async database =>
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(database.Database.GetConnectionString())
                .AddInterceptors(interceptor)
                .Options;
            await using var interceptedDatabase = new AppDbContext(options);
            var service = new AccountService(interceptedDatabase, TimeProvider.System);

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                service.CreateAccountAsync(
                    _customerId,
                    new CreateAccountRequest { AccountType = AccountType.Savings },
                    cancellation.Token
                )
            );
            Assert.Equal(1, interceptor.Attempts);
        });
        await AssertOnlyOriginalAccountAsync();
    }

    private WebApplicationFactory<Program> WithInterceptor(SaveChangesInterceptor interceptor) =>
        _factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
                services.AddDbContext<AppDbContext>(options => options.AddInterceptors(interceptor))
            )
        );

    private static HttpClient CreateClient(WebApplicationFactory<Program> factory) =>
        factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost"),
                AllowAutoRedirect = false,
            }
        );

    private Task AssertOnlyOriginalAccountAsync() =>
        _factory.InDatabaseAsync(async database =>
        {
            var account = await database.Accounts.SingleAsync();
            Assert.Equal(_existingAccountId, account.Id);
            Assert.Equal(ExistingAccountNumber, account.AccountNumber);
            Assert.Equal(25m, account.Balance);
        });

    // Force real SQLite constraints without replacing the production number generator.
    private sealed class AccountSaveInterceptor(
        int collisions,
        Guid? conflictingId = null,
        CancellationTokenSource? cancelOnFailure = null
    ) : SaveChangesInterceptor
    {
        public int Attempts { get; private set; }
        public Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry<Account>? LastEntry
        {
            get;
            private set;
        }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default
        )
        {
            var entry = eventData
                .Context!.ChangeTracker.Entries<Account>()
                .Single(item => item.State == EntityState.Added);
            LastEntry = entry;
            Attempts++;

            if (conflictingId.HasValue)
            {
                entry.Entity.Id = conflictingId.Value;
            }
            else if (Attempts <= collisions)
            {
                entry.Entity.AccountNumber = ExistingAccountNumber;
            }

            return ValueTask.FromResult(result);
        }

        public override Task SaveChangesFailedAsync(
            DbContextErrorEventData eventData,
            CancellationToken cancellationToken = default
        )
        {
            cancelOnFailure?.Cancel();
            return Task.CompletedTask;
        }
    }
}

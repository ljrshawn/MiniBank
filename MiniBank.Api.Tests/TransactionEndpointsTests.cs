using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text;
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

public sealed class TransactionEndpointsTests : IAsyncLifetime
{
    private readonly MiniBankApiFactory _factory = new();
    private readonly Guid _accountId = Guid.NewGuid();
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        _client = _factory.CreateApiClient();
        await _factory.InitializeDatabaseAsync();
        await _factory.InDatabaseAsync(async database =>
        {
            var customer = new Customer
            {
                Id = Guid.NewGuid(),
                FirstName = "Test",
                LastName = "Customer",
                Email = "transactions@example.com",
                PasswordHash = "test-only-placeholder",
                PhoneNumber = "+61 412 345 678",
                CreatedAt = DateTime.UtcNow,
            };
            database.Customers.Add(customer);
            database.Accounts.Add(
                new Account
                {
                    Id = _accountId,
                    CustomerId = customer.Id,
                    AccountNumber = "123456789",
                    AccountType = AccountType.Checking,
                    Balance = 100m,
                    CreatedAt = DateTime.UtcNow,
                }
            );
            await database.SaveChangesAsync();
        });
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    [Theory]
    [InlineData("deposit", TransactionType.Deposit)]
    [InlineData("withdraw", TransactionType.Withdrawal)]
    public async Task Transaction_persists_one_history_entry_and_updates_balance(
        string operation,
        TransactionType transactionType
    )
    {
        var beforeTransaction = DateTime.UtcNow;
        using var response = await PostAsync(
            operation,
            new { amount = 25.75m, description = "  Test payment  " }
        );

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var transaction = await response.Content.ReadFromJsonAsync<TransactionResponse>();
        Assert.NotNull(transaction);
        Assert.NotEqual(Guid.Empty, transaction.Id);
        Assert.Equal(_accountId, transaction.AccountId);
        Assert.Equal(transactionType, transaction.TransactionType);
        Assert.Equal(25.75m, transaction.Amount);
        Assert.Equal(operation == "deposit" ? 125.75m : 74.25m, transaction.BalanceAfterTransaction);
        Assert.Equal("Test payment", transaction.Description);
        Assert.Equal(transaction.TransactionDate, transaction.CreatedAt);
        Assert.Equal(DateTimeKind.Utc, transaction.TransactionDate.Kind);
        Assert.InRange(transaction.CreatedAt, beforeTransaction, DateTime.UtcNow);

        var history = await GetHistoryAsync();
        Assert.Equal(transaction, Assert.Single(history!));
        var account = await _client.GetFromJsonAsync<AccountResponse>($"/api/accounts/{_accountId}");
        Assert.Equal(transaction.BalanceAfterTransaction, account!.Balance);
        await AssertStateAsync(transaction.BalanceAfterTransaction, 1);
    }

    [Theory]
    [InlineData("deposit", "{}")]
    [InlineData("withdraw", "{}")]
    [InlineData("deposit", "{\"amount\":null}")]
    [InlineData("withdraw", "{\"amount\":null}")]
    [InlineData("deposit", "{\"amount\":0}")]
    [InlineData("withdraw", "{\"amount\":0}")]
    [InlineData("deposit", "{\"amount\":-1}")]
    [InlineData("withdraw", "{\"amount\":-1}")]
    [InlineData("deposit", "{\"amount\":1000000000000}")]
    [InlineData("withdraw", "{\"amount\":1000000000000}")]
    [InlineData("deposit", "{\"amount\":1,\"balanceAfterTransaction\":999}")]
    [InlineData("withdraw", "{\"amount\":1,\"transactionType\":0}")]
    [InlineData("deposit", "null")]
    [InlineData("withdraw", "{")]
    public async Task Invalid_request_does_not_change_balance_or_history(
        string operation,
        string json
    )
    {
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        using var response = await _client.PostAsync(
            $"/api/accounts/{_accountId}/{operation}",
            content
        );

        await AssertProblemAsync(response, HttpStatusCode.BadRequest);
        await AssertStateAsync(100m, 0);
    }

    [Theory]
    [InlineData("deposit", null, "Deposit")]
    [InlineData("deposit", "   ", "Deposit")]
    [InlineData("withdraw", null, "Withdrawal")]
    [InlineData("withdraw", "", "Withdrawal")]
    public async Task Empty_description_uses_the_operation_name(
        string operation,
        string? description,
        string expected
    )
    {
        using var response = await PostAsync(operation, new { amount = 1m, description });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var transaction = await response.Content.ReadFromJsonAsync<TransactionResponse>();
        Assert.Equal(expected, transaction!.Description);
    }

    [Theory]
    [InlineData("deposit")]
    [InlineData("withdraw")]
    public async Task Description_is_limited_to_255_characters(string operation)
    {
        using var invalid = await PostAsync(
            operation,
            new { amount = 1m, description = new string('x', 256) }
        );
        await AssertProblemAsync(invalid, HttpStatusCode.BadRequest);
        await AssertStateAsync(100m, 0);

        using var valid = await PostAsync(
            operation,
            new { amount = 1m, description = new string('x', 255) }
        );
        Assert.Equal(HttpStatusCode.OK, valid.StatusCode);
    }

    [Theory]
    [InlineData("deposit", "0.001")]
    [InlineData("withdraw", "0.001")]
    [InlineData("deposit", "999999999999")]
    [InlineData("withdraw", "100")]
    public async Task Valid_boundary_amounts_are_accepted(string operation, string amountText)
    {
        var amount = decimal.Parse(amountText, CultureInfo.InvariantCulture);
        using var response = await PostAsync(operation, new { amount });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var transaction = await response.Content.ReadFromJsonAsync<TransactionResponse>();
        Assert.Equal(amount, transaction!.Amount);
        await AssertStateAsync(operation == "deposit" ? 100m + amount : 100m - amount, 1);
    }

    [Fact]
    public async Task Insufficient_funds_does_not_change_balance_or_history()
    {
        using var response = await PostAsync("withdraw", new { amount = 100.01m });

        var problem = await AssertProblemAsync(response, HttpStatusCode.BadRequest);
        Assert.Equal("Insufficient funds", problem["title"]!.GetValue<string>());
        await AssertStateAsync(100m, 0);
    }

    [Fact]
    public async Task Deposit_rejects_balance_overflow_without_creating_history()
    {
        await _factory.InDatabaseAsync(async database =>
        {
            (await database.Accounts.SingleAsync()).Balance = decimal.MaxValue;
            await database.SaveChangesAsync();
        });

        using var response = await PostAsync("deposit", new { amount = 1m });

        var problem = await AssertProblemAsync(response, HttpStatusCode.BadRequest);
        Assert.Equal("Balance limit exceeded", problem["title"]!.GetValue<string>());
        await AssertStateAsync(decimal.MaxValue, 0);
    }

    [Theory]
    [InlineData("deposit")]
    [InlineData("withdraw")]
    public async Task Transactions_return_not_found_for_unknown_accounts(string operation)
    {
        using var response = await _client.PostAsJsonAsync(
            $"/api/accounts/{Guid.NewGuid()}/{operation}",
            new { amount = 1m }
        );

        await AssertProblemAsync(response, HttpStatusCode.NotFound);
        await AssertStateAsync(100m, 0);
    }

    [Fact]
    public async Task History_distinguishes_empty_accounts_from_missing_accounts()
    {
        var history = await GetHistoryAsync();
        Assert.Empty(history!);

        using var response = await _client.GetAsync($"/api/accounts/{Guid.NewGuid()}/transactions");
        await AssertProblemAsync(response, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task History_is_scoped_to_the_account_and_orders_ties_consistently()
    {
        var timestamp = new DateTime(2026, 9, 9, 0, 0, 0, DateTimeKind.Utc);
        Guid[] ids =
        [
            Guid.Parse("00000000-0000-0000-0000-000000000001"),
            Guid.Parse("00000000-0000-0000-0000-000000000002"),
            Guid.Parse("00000000-0000-0000-0000-000000000003"),
        ];
        await _factory.InDatabaseAsync(async database =>
        {
            var otherAccount = new Account
            {
                Id = Guid.NewGuid(),
                CustomerId = (await database.Accounts.SingleAsync()).CustomerId,
                AccountNumber = "987654321",
                AccountType = AccountType.Savings,
                CreatedAt = timestamp,
            };
            database.Accounts.Add(otherAccount);
            for (var index = 0; index < ids.Length; index++)
            {
                database.BankTransactions.Add(
                    new BankTransaction
                    {
                        Id = ids[index],
                        AccountId = _accountId,
                        TransactionType = TransactionType.Deposit,
                        Amount = 1m,
                        BalanceAfterTransaction = 101m + index,
                        Description = "Deposit",
                        TransactionDate = index == 0 ? timestamp.AddDays(-1) : timestamp,
                        CreatedAt = timestamp,
                    }
                );
            }
            database.BankTransactions.Add(
                new BankTransaction
                {
                    Id = Guid.NewGuid(),
                    AccountId = otherAccount.Id,
                    TransactionType = TransactionType.Deposit,
                    Amount = 1m,
                    BalanceAfterTransaction = 1m,
                    Description = "Other account",
                    TransactionDate = timestamp.AddDays(1),
                    CreatedAt = timestamp,
                }
            );
            await database.SaveChangesAsync();
        });

        var history = await GetHistoryAsync();

        Assert.NotNull(history);
        Assert.Equal(ids.Reverse(), history.Select(transaction => transaction.Id));
        Assert.All(history, transaction =>
        {
            Assert.Equal(_accountId, transaction.AccountId);
            Assert.Equal(DateTimeKind.Utc, transaction.TransactionDate.Kind);
            Assert.Equal(DateTimeKind.Utc, transaction.CreatedAt.Kind);
        });
    }

    [Theory]
    [InlineData(false, "0")]
    [InlineData(false, "-1")]
    [InlineData(true, "0")]
    [InlineData(true, "-1")]
    [InlineData(false, "1000000000000")]
    [InlineData(true, "1000000000000")]
    public async Task Service_validates_amounts_when_called_without_HTTP(
        bool withdraw,
        string amountText
    )
    {
        await _factory.InDatabaseAsync(async database =>
        {
            var service = new AccountService(database, TimeProvider.System);
            var request = new TransactionRequest
            {
                Amount = decimal.Parse(amountText, CultureInfo.InvariantCulture),
            };

            var result = withdraw
                ? await service.WithdrawAsync(_accountId, request, CancellationToken.None)
                : await service.DepositAsync(_accountId, request, CancellationToken.None);

            Assert.Equal(TransactionStatus.InvalidRequest, result.Status);
            Assert.NotNull(result.ErrorMessage);
            Assert.False(database.ChangeTracker.HasChanges());
        });
        await AssertStateAsync(100m, 0);
    }

    [Theory]
    [InlineData("deposit")]
    [InlineData("withdraw")]
    public async Task Concurrent_balance_change_preserves_the_committed_transaction(
        string operation
    )
    {
        await using var factory = WithInterceptor(new CompetingWithdrawalInterceptor());
        using var client = CreateClient(factory);

        using var response = await client.PostAsJsonAsync(
            $"/api/accounts/{_accountId}/{operation}",
            new { amount = 30m }
        );

        await AssertProblemAsync(response, HttpStatusCode.Conflict);
        await AssertStateAsync(20m, 1);
        await _factory.InDatabaseAsync(async database =>
        {
            var transaction = await database.BankTransactions.SingleAsync();
            Assert.Equal(80m, transaction.Amount);
            Assert.Equal(TransactionType.Withdrawal, transaction.TransactionType);
            Assert.Equal(20m, transaction.BalanceAfterTransaction);
        });
    }

    [Fact]
    public async Task Failed_history_insert_rolls_back_the_balance_update()
    {
        await using var factory = WithInterceptor(new InvalidHistoryInterceptor());
        using var client = CreateClient(factory);

        using var response = await client.PostAsJsonAsync(
            $"/api/accounts/{_accountId}/deposit",
            new { amount = 30m }
        );

        await AssertProblemAsync(response, HttpStatusCode.InternalServerError);
        await AssertStateAsync(100m, 0);
    }

    private Task<HttpResponseMessage> PostAsync(string operation, object request) =>
        _client.PostAsJsonAsync($"/api/accounts/{_accountId}/{operation}", request);

    private Task<List<TransactionResponse>?> GetHistoryAsync() =>
        _client.GetFromJsonAsync<List<TransactionResponse>>($"/api/accounts/{_accountId}/transactions");

    private Task AssertStateAsync(decimal balance, int transactionCount) =>
        _factory.InDatabaseAsync(async database =>
        {
            var account = await database.Accounts.SingleAsync(account => account.Id == _accountId);
            Assert.Equal(balance, account.Balance);
            Assert.Equal(transactionCount, await database.BankTransactions.CountAsync());
        });

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

    private static async Task<JsonObject> AssertProblemAsync(
        HttpResponseMessage response,
        HttpStatusCode expectedStatus
    )
    {
        Assert.Equal(expectedStatus, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<JsonObject>();
        Assert.Equal((int)expectedStatus, problem!["status"]!.GetValue<int>());
        Assert.NotNull(problem["traceId"]);
        return problem;
    }

    private sealed class CompetingWithdrawalInterceptor : SaveChangesInterceptor
    {
        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default
        )
        {
            var database = (AppDbContext)eventData.Context!;
            var account = database
                .ChangeTracker.Entries<Account>()
                .Single(entry => entry.State == EntityState.Modified)
                .Entity;
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(database.Database.GetConnectionString())
                .Options;
            await using var competingDatabase = new AppDbContext(options);
            var service = new AccountService(competingDatabase, TimeProvider.System);

            var competingResult = await service.WithdrawAsync(
                account.Id,
                new TransactionRequest { Amount = 80m },
                cancellationToken
            );
            Assert.Equal(TransactionStatus.Success, competingResult.Status);
            return result;
        }
    }

    private sealed class InvalidHistoryInterceptor : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default
        )
        {
            // Trigger a real NOT NULL constraint failure during the transaction insert.
            var transaction = eventData
                .Context!.ChangeTracker.Entries<BankTransaction>()
                .Single(entry => entry.State == EntityState.Added)
                .Entity;
            transaction.Description = null!;
            return ValueTask.FromResult(result);
        }
    }
}

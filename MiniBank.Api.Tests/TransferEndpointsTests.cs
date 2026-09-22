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
using MiniBank.Api.Features.Transfers;

namespace MiniBank.Api.Tests;

public sealed class TransferEndpointsTests : IAsyncLifetime
{
    private readonly MiniBankApiFactory _factory = new();
    private readonly Guid _fromAccountId = Guid.NewGuid();
    private readonly Guid _toAccountId = Guid.NewGuid();
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
                Email = "transfers@example.com",
                User = new ApplicationUser { CreatedAt = DateTime.UtcNow },
                PhoneNumber = "+61 412 345 678",
                CreatedAt = DateTime.UtcNow,
            };
            database.Customers.Add(customer);
            database.Accounts.AddRange(
                new Account
                {
                    Id = _fromAccountId,
                    CustomerId = customer.Id,
                    AccountNumber = "123456789",
                    AccountType = AccountType.Checking,
                    Balance = 100m,
                    CreatedAt = DateTime.UtcNow,
                },
                new Account
                {
                    Id = _toAccountId,
                    CustomerId = customer.Id,
                    AccountNumber = "987654321",
                    AccountType = AccountType.Savings,
                    Balance = 20m,
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
    [InlineData("  Rent  ", "Rent", "Rent", "Rent")]
    [InlineData(null, "Transfer", "TransferOut", "TransferIn")]
    [InlineData("   ", "Transfer", "TransferOut", "TransferIn")]
    public async Task Transfer_updates_both_balances_and_records_retrievable_history(
        string? description,
        string transferDescription,
        string outgoingDescription,
        string incomingDescription
    )
    {
        var before = DateTime.UtcNow;
        using var response = await _client.PostAsJsonAsync(
            "/api/transfers",
            NewRequest() with { Description = description }
        );

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var transfer = await response.Content.ReadFromJsonAsync<TransferResponse>();
        Assert.NotNull(transfer);
        Assert.NotEqual(Guid.Empty, transfer.Id);
        Assert.Equal(_fromAccountId, transfer.FromAccountId);
        Assert.Equal(_toAccountId, transfer.ToAccountId);
        Assert.Equal(25.75m, transfer.Amount);
        Assert.Equal(transferDescription, transfer.Description);
        Assert.InRange(transfer.CreatedAt, before, DateTime.UtcNow);
        Assert.Equal(transfer.CreatedAt, transfer.TransactionDate);
        Assert.EndsWith($"/api/transfers/{transfer.Id}", response.Headers.Location?.ToString());
        var saved = await _client.GetFromJsonAsync<TransferResponse>(response.Headers.Location);
        Assert.Equal(transfer, saved);
        Assert.Equal(DateTimeKind.Utc, saved!.CreatedAt.Kind);
        Assert.Equal(DateTimeKind.Utc, saved.TransactionDate.Kind);

        await AssertStateAsync(74.25m, 45.75m, transfers: 1, transactions: 2);
        foreach (var (id, type, balance, expectedDescription) in new[]
        {
            (_fromAccountId, TransactionType.TransferOut, 74.25m, outgoingDescription),
            (_toAccountId, TransactionType.TransferIn, 45.75m, incomingDescription),
        })
        {
            var history = await _client.GetFromJsonAsync<List<TransactionResponse>>(
                $"/api/accounts/{id}/transactions"
            );
            var entry = Assert.Single(history!);
            Assert.Equal(type, entry.TransactionType);
            Assert.Equal(25.75m, entry.Amount);
            Assert.Equal(balance, entry.BalanceAfterTransaction);
            Assert.Equal(expectedDescription, entry.Description);
            Assert.Equal(transfer.TransactionDate, entry.TransactionDate);
            Assert.Equal(transfer.CreatedAt, entry.CreatedAt);
        }
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("1000000000000")]
    [InlineData("0.001")]
    public async Task Invalid_amount_is_rejected_by_HTTP_and_service(string amountText)
    {
        var request = NewRequest() with
        {
            Amount = decimal.Parse(amountText, CultureInfo.InvariantCulture),
        };
        using var response = await _client.PostAsJsonAsync("/api/transfers", request);
        await AssertProblemAsync(response, HttpStatusCode.BadRequest);
        await AssertInvalidServiceRequestAsync(request);
        await AssertStateAsync(100m, 20m);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("null")]
    [InlineData("{")]
    public async Task Malformed_request_is_rejected(string json)
    {
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        using var response = await _client.PostAsync("/api/transfers", content);
        await AssertProblemAsync(response, HttpStatusCode.BadRequest);
        await AssertStateAsync(100m, 20m);
    }

    [Fact]
    public async Task Server_controlled_fields_are_rejected()
    {
        using var response = await _client.PostAsJsonAsync(
            "/api/transfers",
            new
            {
                fromAccountId = _fromAccountId,
                toAccountId = _toAccountId,
                amount = 1m,
                transferStatus = TransferStatus.Completed,
            }
        );
        await AssertProblemAsync(response, HttpStatusCode.BadRequest);
        await AssertStateAsync(100m, 20m);
    }

    [Theory]
    [InlineData("same")]
    [InlineData("empty-source")]
    [InlineData("empty-destination")]
    [InlineData("long-description")]
    public async Task Invalid_request_is_rejected_by_HTTP_and_service(string scenario)
    {
        var request = scenario switch
        {
            "same" => NewRequest() with { ToAccountId = _fromAccountId },
            "empty-source" => NewRequest() with { FromAccountId = Guid.Empty },
            "empty-destination" => NewRequest() with { ToAccountId = Guid.Empty },
            _ => NewRequest() with { Description = new string('x', 256) },
        };
        using var response = await _client.PostAsJsonAsync("/api/transfers", request);
        await AssertProblemAsync(response, HttpStatusCode.BadRequest);
        await AssertInvalidServiceRequestAsync(request);
        await AssertStateAsync(100m, 20m);
    }

    [Theory]
    [InlineData("0.01")]
    [InlineData("100")]
    public async Task Valid_boundary_amounts_and_maximum_description_are_accepted(string amountText)
    {
        var amount = decimal.Parse(amountText, CultureInfo.InvariantCulture);
        using var response = await _client.PostAsJsonAsync(
            "/api/transfers",
            NewRequest() with { Amount = amount, Description = new string('x', 255) }
        );
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        await AssertStateAsync(100m - amount, 20m + amount, transfers: 1, transactions: 2);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Missing_account_returns_not_found(bool missingSource)
    {
        var request = missingSource
            ? NewRequest() with { FromAccountId = Guid.NewGuid() }
            : NewRequest() with { ToAccountId = Guid.NewGuid() };
        using var response = await _client.PostAsJsonAsync("/api/transfers", request);
        await AssertProblemAsync(response, HttpStatusCode.NotFound);
        await AssertStateAsync(100m, 20m);
    }

    [Fact]
    public async Task Missing_transfer_returns_not_found()
    {
        using var response = await _client.GetAsync($"/api/transfers/{Guid.NewGuid()}");
        await AssertProblemAsync(response, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Insufficient_funds_preserves_both_accounts()
    {
        using var response = await _client.PostAsJsonAsync(
            "/api/transfers",
            NewRequest() with { Amount = 100.01m }
        );
        var problem = await AssertProblemAsync(response, HttpStatusCode.BadRequest);
        Assert.Equal("Insufficient funds", problem["title"]!.GetValue<string>());
        await AssertStateAsync(100m, 20m);
    }

    [Theory]
    [InlineData("9999999999999999.98")]
    [InlineData("9999999999999999.99")]
    public async Task Destination_balance_limit_preserves_both_accounts(string balanceText)
    {
        var balance = decimal.Parse(balanceText, CultureInfo.InvariantCulture);
        await _factory.InDatabaseAsync(async database =>
        {
            var account = await database.Accounts.SingleAsync(account => account.Id == _toAccountId);
            account.Balance = balance;
            await database.SaveChangesAsync();
        });
        using var response = await _client.PostAsJsonAsync("/api/transfers", NewRequest());
        var problem = await AssertProblemAsync(response, HttpStatusCode.BadRequest);
        Assert.Equal("Balance limit exceeded", problem["title"]!.GetValue<string>());
        await AssertStateAsync(100m, balance);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Concurrent_change_to_either_account_returns_conflict(bool changeSource)
    {
        var competingAccountId = changeSource ? _fromAccountId : _toAccountId;
        await using var factory = WithInterceptor(new CompetingDepositInterceptor(competingAccountId));
        using var client = CreateClient(factory);
        using var response = await client.PostAsJsonAsync("/api/transfers", NewRequest());
        await AssertProblemAsync(response, HttpStatusCode.Conflict);
        await AssertStateAsync(changeSource ? 105m : 100m, changeSource ? 20m : 25m, transactions: 1);
    }

    [Fact]
    public async Task Conflict_clears_pending_changes_before_context_is_reused()
    {
        await using var factory = WithInterceptor(new CompetingDepositInterceptor(_toAccountId));
        await using var scope = factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var service = scope.ServiceProvider.GetRequiredService<TransferService>();
        var result = await service.CreateTransferAsync(NewRequest(), CancellationToken.None);
        Assert.Equal(TransferResultStatus.Conflict, result.Status);
        Assert.False(database.ChangeTracker.HasChanges());
        await database.SaveChangesAsync();
        await AssertStateAsync(100m, 25m, transactions: 1);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Failed_insert_rolls_back_balances_transfer_and_history(bool failTransfer)
    {
        await using var factory = WithInterceptor(new InvalidInsertInterceptor(failTransfer));
        using var client = CreateClient(factory);
        using var response = await client.PostAsJsonAsync("/api/transfers", NewRequest());
        await AssertProblemAsync(response, HttpStatusCode.InternalServerError);
        await AssertStateAsync(100m, 20m);
    }

    [Fact]
    public async Task Cancelled_transfer_propagates_cancellation_and_preserves_accounts()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await _factory.InDatabaseAsync(async database =>
        {
            var service = new TransferService(database, TimeProvider.System);
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                service.CreateTransferAsync(NewRequest(), cancellation.Token)
            );
        });
        await AssertStateAsync(100m, 20m);
    }

    private CreateTransferRequest NewRequest() =>
        new()
        {
            FromAccountId = _fromAccountId,
            ToAccountId = _toAccountId,
            Amount = 25.75m,
        };

    private Task AssertInvalidServiceRequestAsync(CreateTransferRequest request) =>
        _factory.InDatabaseAsync(async database =>
        {
            var service = new TransferService(database, TimeProvider.System);
            var result = await service.CreateTransferAsync(request, CancellationToken.None);
            Assert.Equal(TransferResultStatus.InvalidRequest, result.Status);
            Assert.NotNull(result.ErrorMessage);
            Assert.False(database.ChangeTracker.HasChanges());
        });

    private Task AssertStateAsync(
        decimal fromBalance,
        decimal toBalance,
        int transfers = 0,
        int transactions = 0
    ) =>
        _factory.InDatabaseAsync(async database =>
        {
            var accounts = await database.Accounts.ToDictionaryAsync(account => account.Id);
            Assert.Equal(fromBalance, accounts[_fromAccountId].Balance);
            Assert.Equal(toBalance, accounts[_toAccountId].Balance);
            Assert.Equal(transfers, await database.Transfers.CountAsync());
            Assert.Equal(transactions, await database.BankTransactions.CountAsync());
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
        HttpStatusCode status
    )
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<JsonObject>();
        Assert.Equal((int)status, problem!["status"]!.GetValue<int>());
        Assert.NotNull(problem["traceId"]);
        return problem;
    }

    private sealed class CompetingDepositInterceptor(Guid accountId) : SaveChangesInterceptor
    {
        private bool _hasRun;

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default
        )
        {
            if (_hasRun)
            {
                return result;
            }
            _hasRun = true;
            var database = (AppDbContext)eventData.Context!;
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseNpgsql(database.Database.GetConnectionString())
                .Options;
            await using var competingDatabase = new AppDbContext(options);
            var service = new AccountService(competingDatabase, TimeProvider.System);
            var deposit = await service.DepositAsync(
                accountId,
                new TransactionRequest { Amount = 5m },
                cancellationToken
            );
            Assert.Equal(TransactionStatus.Success, deposit.Status);
            return result;
        }
    }

    private sealed class InvalidInsertInterceptor(bool failTransfer) : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default
        )
        {
            // Trigger a real NOT NULL failure in either the transfer or one history entry.
            if (failTransfer)
            {
                var transfer = eventData.Context!.ChangeTracker.Entries<Transfer>().Single().Entity;
                transfer.Description = null!;
            }
            else
            {
                var transaction = eventData.Context!.ChangeTracker.Entries<BankTransaction>()
                    .Single(entry => entry.Entity.TransactionType == TransactionType.TransferIn)
                    .Entity;
                transaction.Description = null!;
            }
            return ValueTask.FromResult(result);
        }
    }
}

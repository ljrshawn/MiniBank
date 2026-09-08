using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using MiniBank.Api.Data;
using MiniBank.Api.Entities;
using MiniBank.Api.Enums;
using MiniBank.Api.Features.Accounts;
using MiniBank.Api.Features.Customers;

namespace MiniBank.Api.Tests;

public sealed class AccountEndpointsTests : IAsyncLifetime
{
    private readonly MiniBankApiFactory _factory = new();
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        _client = _factory.CreateApiClient();
        await _factory.InitializeDatabaseAsync();
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    [Theory]
    [InlineData(AccountType.Checking)]
    [InlineData(AccountType.Savings)]
    public async Task Create_persists_account_for_route_customer_and_returns_retrievable_location(
        AccountType accountType
    )
    {
        var customer = await CreateCustomerAsync();
        var beforeCreation = DateTime.UtcNow;

        using var response = await _client.PostAsJsonAsync(
            $"/api/accounts/customers/{customer.Id}",
            new CreateAccountRequest { AccountType = accountType }
        );

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var account = await response.Content.ReadFromJsonAsync<AccountResponse>();
        Assert.NotNull(account);
        Assert.NotEqual(Guid.Empty, account.Id);
        Assert.Equal(customer.Id, account.CustomerId);
        Assert.Equal(accountType, account.AccountType);
        Assert.Equal(AccountStatus.Active, account.AccountStatus);
        Assert.Equal(0m, account.Balance);
        Assert.Matches("^[1-9][0-9]{8}$", account.AccountNumber);
        Assert.Equal(DateTimeKind.Utc, account.CreatedAt.Kind);
        Assert.InRange(account.CreatedAt, beforeCreation, DateTime.UtcNow);
        Assert.EndsWith($"/api/accounts/{account.Id}", response.Headers.Location!.ToString());

        using var retrieved = await _client.GetAsync(response.Headers.Location);
        Assert.Equal(HttpStatusCode.OK, retrieved.StatusCode);
        Assert.Equal(account, await retrieved.Content.ReadFromJsonAsync<AccountResponse>());

        await _factory.InDatabaseAsync(async database =>
        {
            var stored = await database.Accounts.SingleAsync();
            Assert.Equal(account.Id, stored.Id);
            Assert.Equal(customer.Id, stored.CustomerId);
            Assert.Equal(account.AccountNumber, stored.AccountNumber);
            Assert.Equal(accountType, stored.AccountType);
            Assert.Equal(0m, stored.Balance);
        });
    }

    [Fact]
    public async Task Create_returns_not_found_for_unknown_customer_without_creating_an_account()
    {
        using var response = await _client.PostAsJsonAsync(
            $"/api/accounts/customers/{Guid.NewGuid()}",
            new CreateAccountRequest { AccountType = AccountType.Checking }
        );

        await AssertProblemAsync(response, HttpStatusCode.NotFound);
        await AssertNoAccountsAsync();
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("null")]
    [InlineData("{")]
    [InlineData("")]
    [InlineData("{\"accountType\":null}")]
    [InlineData("{\"accountType\":-1}")]
    [InlineData("{\"accountType\":999}")]
    [InlineData("{\"accountType\":\"Savings\"}")]
    public async Task Create_rejects_invalid_request_without_persisting_it(string json)
    {
        var customer = await CreateCustomerAsync();
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        using var response = await _client.PostAsync(
            $"/api/accounts/customers/{customer.Id}",
            content
        );

        await AssertProblemAsync(response, HttpStatusCode.BadRequest);
        await AssertNoAccountsAsync();
    }

    [Theory]
    [InlineData("balance", "1000")]
    [InlineData("customerId", "\"00000000-0000-0000-0000-000000000001\"")]
    [InlineData("id", "\"00000000-0000-0000-0000-000000000001\"")]
    [InlineData("accountNumber", "\"12345\"")]
    [InlineData("accountStatus", "2")]
    [InlineData("createdAt", "\"2020-01-01T00:00:00Z\"")]
    public async Task Create_rejects_fields_controlled_by_the_server(
        string property,
        string jsonValue
    )
    {
        var customer = await CreateCustomerAsync();
        var payload = new JsonObject
        {
            ["accountType"] = 0,
            [property] = JsonNode.Parse(jsonValue),
        };

        using var response = await _client.PostAsJsonAsync(
            $"/api/accounts/customers/{customer.Id}",
            payload
        );

        await AssertProblemAsync(response, HttpStatusCode.BadRequest);
        await AssertNoAccountsAsync();
    }

    [Fact]
    public async Task Create_allows_multiple_accounts_with_distinct_numbers()
    {
        var customer = await CreateCustomerAsync();
        var responses = await Task.WhenAll(
            _client.PostAsJsonAsync(
                $"/api/accounts/customers/{customer.Id}",
                new CreateAccountRequest { AccountType = AccountType.Checking }
            ),
            _client.PostAsJsonAsync(
                $"/api/accounts/customers/{customer.Id}",
                new CreateAccountRequest { AccountType = AccountType.Savings }
            )
        );

        try
        {
            Assert.All(
                responses,
                response => Assert.Equal(HttpStatusCode.Created, response.StatusCode)
            );
            await _factory.InDatabaseAsync(async database =>
            {
                var accounts = await database.Accounts.ToListAsync();
                Assert.Equal(2, accounts.Count);
                Assert.Equal(
                    2,
                    accounts.Select(account => account.AccountNumber).Distinct().Count()
                );
                Assert.All(accounts, account => Assert.Equal(customer.Id, account.CustomerId));
            });
        }
        finally
        {
            foreach (var response in responses)
            {
                response.Dispose();
            }
        }
    }

    [Fact]
    public async Task Get_returns_not_found_for_unknown_account()
    {
        using var response = await _client.GetAsync($"/api/accounts/{Guid.NewGuid()}");
        await AssertProblemAsync(response, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Created_account_prevents_cascade_deletion_of_its_customer()
    {
        var customer = await CreateCustomerAsync();
        using var created = await _client.PostAsJsonAsync(
            $"/api/accounts/customers/{customer.Id}",
            new CreateAccountRequest { AccountType = AccountType.Checking }
        );
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        using var deleted = await _client.DeleteAsync($"/api/customers/{customer.Id}");
        await AssertProblemAsync(deleted, HttpStatusCode.Conflict);
        await _factory.InDatabaseAsync(async database =>
        {
            Assert.Equal(1, await database.Accounts.CountAsync());
            Assert.Equal(1, await database.Customers.CountAsync());
        });
    }

    [Fact]
    public async Task Create_handles_customer_deleted_between_lookup_and_save()
    {
        var customer = await CreateCustomerAsync();
        await _factory.InDatabaseAsync(async database =>
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(database.Database.GetConnectionString())
                .AddInterceptors(new DeleteCustomerBeforeAccountSaveInterceptor())
                .Options;
            await using var concurrentDatabase = new AppDbContext(options);
            var service = new AccountService(concurrentDatabase, TimeProvider.System);

            var result = await service.CreateAccountAsync(
                customer.Id,
                new CreateAccountRequest { AccountType = AccountType.Checking },
                CancellationToken.None
            );

            Assert.Null(result);
            Assert.Equal(0, await database.Accounts.CountAsync());
            Assert.Equal(0, await database.Customers.CountAsync());
        });
    }

    private async Task<CustomerResponse> CreateCustomerAsync()
    {
        using var response = await _client.PostAsJsonAsync(
            "/api/customers",
            new CreateCustomerRequest
            {
                FirstName = "David",
                LastName = "Smith",
                Email = $"{Guid.NewGuid():N}@example.com",
                Password = "A-long-test-password!",
                PhoneNumber = "+61 412 345 678",
            }
        );
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<CustomerResponse>())!;
    }

    private Task AssertNoAccountsAsync() =>
        _factory.InDatabaseAsync(async database =>
            Assert.Equal(0, await database.Accounts.CountAsync())
        );

    private static async Task AssertProblemAsync(
        HttpResponseMessage response,
        HttpStatusCode expectedStatus
    )
    {
        Assert.Equal(expectedStatus, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<JsonObject>();
        Assert.Equal((int)expectedStatus, problem!["status"]!.GetValue<int>());
        Assert.NotNull(problem["traceId"]);
    }

    private sealed class DeleteCustomerBeforeAccountSaveInterceptor : SaveChangesInterceptor
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
                .Single(entry => entry.State == EntityState.Added)
                .Entity;
            await database
                .Customers.Where(customer => customer.Id == account.CustomerId)
                .ExecuteDeleteAsync(cancellationToken);
            return result;
        }
    }
}

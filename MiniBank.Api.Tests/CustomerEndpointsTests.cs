using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MiniBank.Api.Entities;
using MiniBank.Api.Enums;
using MiniBank.Api.Features.Customers;

namespace MiniBank.Api.Tests;

public sealed class CustomerEndpointsTests : IAsyncLifetime
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

    [Fact]
    public async Task Create_returns_retrievable_resource_and_stores_a_password_hash()
    {
        var request = ValidRequest() with { FirstName = "  David  ", TaxFileNumber = "123456789" };
        using var response = await _client.PostAsJsonAsync("/customers", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var customer = await response.Content.ReadFromJsonAsync<CustomerResponse>();
        Assert.NotNull(customer);
        Assert.Equal("David", customer.FirstName);
        Assert.Equal(CustomerStatus.Active, customer.CustomerStatus);
        Assert.Equal(DateTimeKind.Utc, customer.CreatedAt.Kind);
        Assert.EndsWith($"/customers/{customer.Id}", response.Headers.Location!.ToString());

        using var fetched = await _client.GetAsync(response.Headers.Location);
        Assert.Equal(HttpStatusCode.OK, fetched.StatusCode);
        Assert.Equal(customer, await fetched.Content.ReadFromJsonAsync<CustomerResponse>());

        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("password", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("taxFileNumber", body, StringComparison.OrdinalIgnoreCase);

        await _factory.InDatabaseAsync(async database =>
        {
            var stored = await database.Customers.SingleAsync();
            Assert.NotEqual(request.Password, stored.PasswordHash);
            Assert.Equal(
                PasswordVerificationResult.Success,
                new PasswordHasher<Customer>().VerifyHashedPassword(
                    stored,
                    stored.PasswordHash,
                    request.Password
                )
            );
            Assert.Equal(request.TaxFileNumber, stored.TaxFileNumber);
        });
    }

    [Theory]
    [InlineData("firstName", "")]
    [InlineData("firstName", "   ")]
    [InlineData("firstName", null)]
    [InlineData("lastName", "")]
    [InlineData("email", "not-an-email")]
    [InlineData("password", "short")]
    [InlineData("password", null)]
    [InlineData("phoneNumber", "invalid")]
    [InlineData("taxFileNumber", "12345678901")]
    public async Task Create_rejects_invalid_input_without_persisting_it(
        string property,
        string? value
    )
    {
        var payload = JsonSerializer
            .SerializeToNode(ValidRequest(), JsonSerializerOptions.Web)!
            .AsObject();
        payload[property] = value;
        using var response = await _client.PostAsJsonAsync("/customers", payload);

        var problem = await AssertProblemAsync(response, HttpStatusCode.BadRequest);
        Assert.NotEmpty(problem["errors"]!.AsObject());
        await _factory.InDatabaseAsync(async database =>
            Assert.Equal(0, await database.Customers.CountAsync())
        );
    }

    [Fact]
    public async Task Create_accepts_legacy_password_property_casing()
    {
        var payload = JsonSerializer
            .SerializeToNode(ValidRequest(), JsonSerializerOptions.Web)!
            .AsObject();
        var password = payload["password"]!.GetValue<string>();
        payload.Remove("password");
        payload["passWord"] = password;

        using var response = await _client.PostAsJsonAsync("/customers", payload);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Theory]
    [InlineData("DAVID.SMITH@example.com")]
    [InlineData(" david.smith@example.com ")]
    public async Task Create_rejects_duplicate_email(string email)
    {
        await CreateCustomerAsync(ValidRequest());
        using var response = await _client.PostAsJsonAsync(
            "/customers",
            ValidRequest() with
            {
                Email = email,
            }
        );
        await AssertProblemAsync(response, HttpStatusCode.Conflict);
        await _factory.InDatabaseAsync(async database =>
            Assert.Equal(1, await database.Customers.CountAsync())
        );
    }

    [Fact]
    public async Task Concurrent_creates_allow_only_one_customer_with_the_same_email()
    {
        var responses = await Task.WhenAll(
            _client.PostAsJsonAsync("/customers", ValidRequest()),
            _client.PostAsJsonAsync(
                "/customers",
                ValidRequest() with
                {
                    Email = "DAVID.SMITH@example.com",
                }
            )
        );

        try
        {
            Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Created);
            Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Conflict);
            await _factory.InDatabaseAsync(async database =>
                Assert.Equal(1, await database.Customers.CountAsync())
            );
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
    public async Task Update_changes_profile_and_status_without_changing_credentials()
    {
        var created = await CreateCustomerAsync(ValidRequest());
        var update = ValidUpdate(created.Email) with
        {
            LastName = "Jones",
            CustomerStatus = CustomerStatus.Suspended,
        };

        using var response = await _client.PutAsJsonAsync($"/customers/{created.Id}", update);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var customer = await response.Content.ReadFromJsonAsync<CustomerResponse>();
        Assert.NotNull(customer);
        Assert.Equal("Jones", customer.LastName);
        Assert.Equal(CustomerStatus.Suspended, customer.CustomerStatus);
        Assert.Equal(created.CreatedAt, customer.CreatedAt);

        await _factory.InDatabaseAsync(async database =>
        {
            var stored = await database.Customers.SingleAsync();
            Assert.Equal(
                PasswordVerificationResult.Success,
                new PasswordHasher<Customer>().VerifyHashedPassword(
                    stored,
                    stored.PasswordHash,
                    ValidRequest().Password
                )
            );
        });
    }

    [Theory]
    [InlineData(null)]
    [InlineData(999)]
    public async Task Update_rejects_missing_or_unknown_status(int? status)
    {
        var customer = await CreateCustomerAsync(ValidRequest());
        var payload = JsonSerializer
            .SerializeToNode(ValidUpdate(customer.Email), JsonSerializerOptions.Web)!
            .AsObject();
        if (status.HasValue)
        {
            payload["customerStatus"] = status.Value;
        }
        else
        {
            payload.Remove("customerStatus");
        }

        using var response = await _client.PutAsJsonAsync($"/customers/{customer.Id}", payload);
        await AssertProblemAsync(response, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Update_rejects_another_customers_email_without_changing_the_profile()
    {
        await CreateCustomerAsync(ValidRequest());
        var second = await CreateCustomerAsync(
            ValidRequest() with
            {
                Email = "second@example.com",
            }
        );

        using var response = await _client.PutAsJsonAsync(
            $"/customers/{second.Id}",
            ValidUpdate("DAVID.SMITH@example.com") with
            {
                LastName = "Changed",
            }
        );

        await AssertProblemAsync(response, HttpStatusCode.Conflict);
        Assert.Equal(
            second,
            await _client.GetFromJsonAsync<CustomerResponse>($"/customers/{second.Id}")
        );
    }

    [Theory]
    [InlineData("GET")]
    [InlineData("PUT")]
    [InlineData("DELETE")]
    public async Task Missing_customer_returns_not_found(string method)
    {
        using var request = new HttpRequestMessage(
            new HttpMethod(method),
            $"/customers/{Guid.NewGuid()}"
        );
        if (method == "PUT")
        {
            request.Content = JsonContent.Create(ValidUpdate("unknown@example.com"));
        }

        using var response = await _client.SendAsync(request);
        await AssertProblemAsync(response, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Delete_removes_customer_and_subsequent_delete_returns_not_found()
    {
        var customer = await CreateCustomerAsync(ValidRequest());
        using var response = await _client.DeleteAsync($"/customers/{customer.Id}");
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Empty(await response.Content.ReadAsStringAsync());

        using var repeated = await _client.DeleteAsync($"/customers/{customer.Id}");
        await AssertProblemAsync(repeated, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Delete_preserves_customer_and_accounts_when_accounts_exist()
    {
        var customer = await CreateCustomerAsync(ValidRequest());
        await _factory.InDatabaseAsync(async database =>
        {
            database.Accounts.Add(
                new Account
                {
                    Id = Guid.NewGuid(),
                    CustomerId = customer.Id,
                    AccountNumber = "1000000001",
                    AccountType = AccountType.Savings,
                    Balance = 123.45m,
                    CreatedAt = DateTime.UtcNow,
                }
            );
            await database.SaveChangesAsync();
        });

        using var response = await _client.DeleteAsync($"/customers/{customer.Id}");
        await AssertProblemAsync(response, HttpStatusCode.Conflict);
        await _factory.InDatabaseAsync(async database =>
        {
            Assert.Equal(1, await database.Customers.CountAsync());
            Assert.Equal(123.45m, (await database.Accounts.SingleAsync()).Balance);
        });
    }

    [Fact]
    public async Task List_returns_stable_non_overlapping_pages()
    {
        var customers = new List<CustomerResponse>();
        for (var index = 0; index < 3; index++)
        {
            customers.Add(
                await CreateCustomerAsync(
                    ValidRequest() with
                    {
                        Email = $"customer{index}@example.com",
                    }
                )
            );
        }

        var first = await _client.GetFromJsonAsync<List<CustomerResponse>>(
            "/customers?page=1&pageSize=2"
        );
        var second = await _client.GetFromJsonAsync<List<CustomerResponse>>(
            "/customers?page=2&pageSize=2"
        );
        var empty = await _client.GetFromJsonAsync<List<CustomerResponse>>(
            "/customers?page=3&pageSize=2"
        );

        Assert.Equal(customers.Take(2), first);
        Assert.Equal(customers.Skip(2), second);
        Assert.Empty(empty!);
    }

    [Theory]
    [InlineData("page=0")]
    [InlineData("page=1000001")]
    [InlineData("pageSize=0")]
    [InlineData("pageSize=101")]
    [InlineData("page=abc")]
    public async Task List_rejects_invalid_pagination(string query)
    {
        using var response = await _client.GetAsync($"/customers?{query}");
        await AssertProblemAsync(response, HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData("{")]
    [InlineData("null")]
    [InlineData("{}")]
    public async Task Malformed_or_missing_body_returns_bad_request(string body)
    {
        using var content = new StringContent(body, Encoding.UTF8, "application/json");
        using var response = await _client.PostAsync("/customers", content);
        await AssertProblemAsync(response, HttpStatusCode.BadRequest);
    }

    private async Task<CustomerResponse> CreateCustomerAsync(CreateCustomerRequest request)
    {
        using var response = await _client.PostAsJsonAsync("/customers", request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<CustomerResponse>())!;
    }

    private static CreateCustomerRequest ValidRequest() =>
        new()
        {
            FirstName = "David",
            LastName = "Smith",
            Email = "david.smith@example.com",
            Password = "A-long-test-password!",
            PhoneNumber = "+61 412 345 678",
        };

    private static UpdateCustomerRequest ValidUpdate(string email) =>
        new()
        {
            FirstName = "David",
            LastName = "Smith",
            Email = email,
            PhoneNumber = "+61 412 345 678",
            CustomerStatus = CustomerStatus.Active,
        };

    private static async Task<JsonObject> AssertProblemAsync(
        HttpResponseMessage response,
        HttpStatusCode expectedStatus
    )
    {
        Assert.Equal(expectedStatus, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<JsonObject>();
        Assert.Equal((int)expectedStatus, problem!["status"]!.GetValue<int>());
        Assert.False(string.IsNullOrWhiteSpace(problem["traceId"]!.GetValue<string>()));
        return problem;
    }
}

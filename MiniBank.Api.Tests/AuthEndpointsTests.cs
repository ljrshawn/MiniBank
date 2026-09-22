using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using MiniBank.Api.Data;
using MiniBank.Api.Entities;
using MiniBank.Api.Features.Auth;
using MiniBank.Api.Features.Customers;

namespace MiniBank.Api.Tests;

public sealed class AuthEndpointsTests : IAsyncLifetime
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
    public async Task Register_creates_linked_identity_and_customer_and_returns_a_valid_token()
    {
        var request = ValidRequest() with { FirstName = "  David  ", Email = " david@example.com " };
        using var response = await _client.PostAsJsonAsync("/api/auth/register", request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(auth);
        Assert.Equal("Bearer", auth.TokenType);
        Assert.Equal(3_600, auth.ExpiresIn);
        var handler = new JwtSecurityTokenHandler { MapInboundClaims = false };
        var principal = handler.ValidateToken(auth.AccessToken, new TokenValidationParameters
        {
            ValidIssuer = "MiniBank.Api",
            ValidAudience = "MiniBank.Client",
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(MiniBankApiFactory.SigningKey)),
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero,
            ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
        }, out var token);
        Assert.Equal(TimeSpan.FromSeconds(auth.ExpiresIn), token.ValidTo - token.ValidFrom);
        Assert.Equal("david@example.com", principal.FindFirst(JwtRegisteredClaimNames.Email)?.Value);
        Assert.NotNull(principal.FindFirst(JwtRegisteredClaimNames.Jti));

        await _factory.InDatabaseAsync(async database =>
        {
            var customer = await database.Customers.Include(customer => customer.User).SingleAsync();
            Assert.Equal("David", customer.FirstName);
            Assert.Equal("david@example.com", customer.Email);
            Assert.Equal(request.PhoneNumber, customer.PhoneNumber);
            Assert.Equal(customer.User.Id, principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value);
            Assert.Equal(customer.CreatedAt, customer.User.CreatedAt);
            Assert.Equal("DAVID@EXAMPLE.COM", customer.User.NormalizedEmail);
            Assert.Equal(PasswordVerificationResult.Success,
                new PasswordHasher<ApplicationUser>().VerifyHashedPassword(
                    customer.User, customer.User.PasswordHash!, request.Password));
        });
    }

    [Theory]
    [InlineData("password", "no-digits-or-uppercase!")]
    [InlineData("password", "short")]
    [InlineData("email", "invalid-email")]
    [InlineData("firstName", "   ")]
    [InlineData("phoneNumber", null)]
    public async Task Register_rejects_invalid_input_without_saving_a_user(string field, string? value)
    {
        var payload = System.Text.Json.JsonSerializer.SerializeToNode(
            ValidRequest(), System.Text.Json.JsonSerializerOptions.Web)!.AsObject();
        payload[field] = value;
        using var response = await _client.PostAsJsonAsync("/api/auth/register", payload);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonObject>();
        Assert.NotEmpty(problem!["errors"]!.AsObject());
        Assert.NotNull(problem["traceId"]);
        await AssertCountsAsync(0);
    }

    [Fact]
    public async Task Concurrent_registration_returns_one_success_and_one_conflict()
    {
        var responses = await Task.WhenAll(
            _client.PostAsJsonAsync("/api/auth/register", ValidRequest()),
            _client.PostAsJsonAsync("/api/auth/register", ValidRequest() with { Email = "DAVID@example.com" })
        );
        try
        {
            Assert.Single(responses, response => response.StatusCode == HttpStatusCode.OK);
            Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Conflict);
            await AssertCountsAsync(1);
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
    public async Task Registration_rejects_email_already_used_by_customer_creation()
    {
        var request = ValidRequest();
        using var created = await _client.PostAsJsonAsync("/api/customers", new CreateCustomerRequest
        {
            FirstName = request.FirstName,
            LastName = request.LastName,
            Email = request.Email,
            Password = request.Password,
            PhoneNumber = request.PhoneNumber,
        });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        using var duplicate = await _client.PostAsJsonAsync("/api/auth/register", request);
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        await AssertCountsAsync(1);
    }

    [Fact]
    public async Task Failed_customer_insert_rolls_back_identity_creation()
    {
        await using var factory = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services => services.AddDbContext<AppDbContext>(options =>
                options.AddInterceptors(new InvalidCustomerInterceptor()))));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
        });
        using var response = await client.PostAsJsonAsync("/api/auth/register", ValidRequest());
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        await AssertCountsAsync(0);
    }

    [Fact]
    public async Task Cancelled_registration_does_not_create_identity_or_customer()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await using var scope = _factory.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<AuthService>();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            service.RegisterAsync(ValidRequest(), cancellation.Token));
        await AssertCountsAsync(0);
    }

    private Task AssertCountsAsync(int count) => _factory.InDatabaseAsync(async database =>
    {
        Assert.Equal(count, await database.Users.CountAsync());
        Assert.Equal(count, await database.Customers.CountAsync());
    });

    private static RegisterRequest ValidRequest() => new()
    {
        Email = "david@example.com",
        Password = "A-long-test-password-1!",
        FirstName = "David",
        LastName = "Smith",
        PhoneNumber = "+61 412 345 678",
    };

    private sealed class InvalidCustomerInterceptor : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default
        )
        {
            eventData.Context!.ChangeTracker.Entries<Customer>().Single().Entity.PhoneNumber = null!;
            return ValueTask.FromResult(result);
        }
    }
}

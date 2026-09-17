using System.Collections.Concurrent;
using System.Data.Common;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MiniBank.Api.Data;

namespace MiniBank.Api.Tests;

public sealed class ApiHttpTests : IAsyncLifetime
{
    private const string MissingId = "11111111-1111-1111-1111-111111111111";
    private readonly MiniBankApiFactory _factory = new();
    private HttpClient _client = null!;

    public Task InitializeAsync()
    {
        _client = _factory.CreateApiClient();
        // No schema: invalid requests must be rejected before a service queries the database.
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    [Theory]
    [InlineData("POST", "/api/customers", "{}", "FirstName")]
    [InlineData("PUT", "/api/customers/{id}", "{}", "CustomerStatus")]
    [InlineData("POST", "/api/accounts/customers/{id}", "{\"accountType\":999}", "AccountType")]
    [InlineData("POST", "/api/accounts/{id}/deposit", "{\"amount\":0}", "Amount")]
    [InlineData("POST", "/api/accounts/{id}/withdraw", "{\"amount\":-1}", "Amount")]
    [InlineData("GET", "/api/customers?page=0", null, "page")]
    [InlineData("GET", "/api/customers?pageSize=101", null, "pageSize")]
    public async Task Validation_uses_the_shared_problem_format_before_calling_services(
        string method,
        string path,
        string? json,
        string field
    )
    {
        using var request = NewRequest(method, path, json);
        using var response = await _client.SendAsync(request);

        var problem = await AssertProblemAsync(response, HttpStatusCode.BadRequest);
        Assert.Equal("One or more validation errors occurred.", problem["title"]!.GetValue<string>());
        var errors = problem["errors"]!.AsObject();
        Assert.Contains(errors, error => error.Key.EndsWith(field, StringComparison.OrdinalIgnoreCase));
        Assert.All(errors, error => Assert.NotEmpty(error.Value!.AsArray()));
    }

    [Fact]
    public async Task Cross_property_validation_uses_the_same_problem_format()
    {
        using var response = await _client.PostAsJsonAsync(
            "/api/transfers",
            new
            {
                fromAccountId = MissingId,
                toAccountId = MissingId,
                amount = 1m,
            }
        );

        var problem = await AssertProblemAsync(response, HttpStatusCode.BadRequest);
        Assert.Equal("One or more validation errors occurred.", problem["title"]!.GetValue<string>());
        Assert.Contains(problem["errors"]!.AsObject(), error => error.Key.EndsWith("ToAccountId"));
    }

    [Theory]
    [InlineData("GET", "/api/unknown", null, "application/json", HttpStatusCode.NotFound)]
    [InlineData("GET", "/api/accounts/not-a-guid", null, "application/json", HttpStatusCode.NotFound)]
    [InlineData("GET", "/api/customers?page=abc", null, "application/json", HttpStatusCode.BadRequest)]
    [InlineData("POST", "/api/customers", "{", "application/json", HttpStatusCode.BadRequest)]
    [InlineData("POST", "/api/customers", "{}", "text/plain", HttpStatusCode.UnsupportedMediaType)]
    [InlineData("PUT", "/api/transfers", "{}", "application/json", HttpStatusCode.MethodNotAllowed)]
    public async Task Binding_and_routing_errors_use_the_shared_problem_format(
        string method,
        string path,
        string? body,
        string contentType,
        HttpStatusCode status
    )
    {
        using var request = NewRequest(method, path, body, contentType);
        using var response = await _client.SendAsync(request);

        await AssertProblemAsync(response, status);
        if (status == HttpStatusCode.MethodNotAllowed)
        {
            Assert.Contains("POST", response.Content.Headers.Allow);
        }
    }

    [Theory]
    [InlineData("/api/customers/{id}", "Customer not found")]
    [InlineData("/api/accounts/{id}", "Account not found")]
    [InlineData("/api/transfers/{id}", "Transfer not found")]
    public async Task Business_errors_keep_their_specific_problem_messages(string path, string title)
    {
        await _factory.InitializeDatabaseAsync();
        using var response = await _client.GetAsync(path.Replace("{id}", MissingId));

        var problem = await AssertProblemAsync(response, HttpStatusCode.NotFound);
        Assert.Equal(title, problem["title"]!.GetValue<string>());
        Assert.False(string.IsNullOrWhiteSpace(problem["detail"]!.GetValue<string>()));
        Assert.False(problem.ContainsKey("errors"));
    }

    [Fact]
    public async Task Unexpected_exception_is_logged_and_returns_a_safe_problem()
    {
        using var logs = new CapturingLoggerProvider();
        await using var factory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureLogging(logging => logging.AddProvider(logs));
            builder.ConfigureServices(services =>
                services.AddDbContext<AppDbContext>(options =>
                    options.AddInterceptors(new FailingQueryInterceptor())
                )
            );
        });
        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost"),
                AllowAutoRedirect = false,
            }
        );

        using var response = await client.GetAsync("/api/customers");

        var problem = await AssertProblemAsync(response, HttpStatusCode.InternalServerError);
        Assert.Equal(
            "An error occurred while processing your request.",
            problem["title"]!.GetValue<string>()
        );
        Assert.False(problem.ContainsKey("exception"));
        Assert.False(problem.ContainsKey("stackTrace"));
        Assert.DoesNotContain(FailingQueryInterceptor.PrivateMessage, problem.ToJsonString());
        Assert.Contains(logs.Entries, entry =>
            entry.Category.EndsWith("ApiExceptionHandler", StringComparison.Ordinal)
            && entry.Level == LogLevel.Error
            && entry.Exception?.Message == FailingQueryInterceptor.PrivateMessage
        );
    }

    private static HttpRequestMessage NewRequest(
        string method,
        string path,
        string? body,
        string contentType = "application/json"
    ) =>
        new(new HttpMethod(method), path.Replace("{id}", MissingId))
        {
            Content = body is null ? null : new StringContent(body, Encoding.UTF8, contentType),
        };

    private static async Task<JsonObject> AssertProblemAsync(
        HttpResponseMessage response,
        HttpStatusCode status
    )
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<JsonObject>();
        Assert.NotNull(problem);
        Assert.Equal((int)status, problem["status"]!.GetValue<int>());
        Assert.Equal(
            response.RequestMessage!.RequestUri!.AbsolutePath,
            problem["instance"]!.GetValue<string>()
        );
        Assert.False(string.IsNullOrWhiteSpace(problem["title"]!.GetValue<string>()));
        Assert.False(string.IsNullOrWhiteSpace(problem["type"]!.GetValue<string>()));
        Assert.False(string.IsNullOrWhiteSpace(problem["traceId"]!.GetValue<string>()));
        return problem;
    }

    private sealed class FailingQueryInterceptor : DbCommandInterceptor
    {
        internal const string PrivateMessage = "Private database connection information.";

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default
        ) => throw new InvalidOperationException(PrivateMessage);
    }

    private sealed class CapturingLoggerProvider : ILoggerProvider
    {
        internal ConcurrentQueue<(string Category, LogLevel Level, Exception? Exception)> Entries
        {
            get;
        } = new();

        public ILogger CreateLogger(string categoryName) => new CapturingLogger(categoryName, this);

        public void Dispose() { }

        private sealed class CapturingLogger(string category, CapturingLoggerProvider provider)
            : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state)
                where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(
                LogLevel logLevel,
                EventId eventId,
                TState state,
                Exception? exception,
                Func<TState, Exception?, string> formatter
            ) => provider.Entries.Enqueue((category, logLevel, exception));
        }
    }
}

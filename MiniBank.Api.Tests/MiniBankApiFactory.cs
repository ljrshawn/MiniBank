using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MiniBank.Api.Data;

namespace MiniBank.Api.Tests;

public sealed class MiniBankApiFactory(string environment = "Testing")
    : WebApplicationFactory<Program>
{
    internal const string SigningKey = "MiniBank-test-signing-key-at-least-32-bytes";
    private readonly PostgresTestDatabase _database = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        _database.Create();
        builder.UseEnvironment(environment);
        builder.UseSetting("ConnectionStrings:DbConnection", _database.ConnectionString);
        builder.UseSetting("Jwt:Key", SigningKey);
        builder.ConfigureLogging(logging => logging.ClearProviders());
    }

    public HttpClient CreateApiClient() =>
        CreateClient(
            new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost"),
                AllowAutoRedirect = false,
            }
        );

    public async Task InDatabaseAsync(Func<AppDbContext, Task> action)
    {
        await using var scope = Services.CreateAsyncScope();
        await action(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    public Task InitializeDatabaseAsync() =>
        InDatabaseAsync(database => database.Database.MigrateAsync());

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        await _database.DisposeAsync();
    }
}

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
    private readonly string _databaseDirectory = Path.Combine(
        Path.GetTempPath(),
        "minibank-tests",
        Guid.NewGuid().ToString("N")
    );

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        Directory.CreateDirectory(_databaseDirectory);
        builder.UseEnvironment(environment);
        builder.UseSetting(
            "ConnectionStrings:DbConnection",
            $"Data Source={Path.Combine(_databaseDirectory, "test.db")};Pooling=False;Foreign Keys=True"
        );
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
        if (Directory.Exists(_databaseDirectory))
        {
            Directory.Delete(_databaseDirectory, recursive: true);
        }
    }
}

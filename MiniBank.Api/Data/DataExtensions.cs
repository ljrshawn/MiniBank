using Microsoft.EntityFrameworkCore;

namespace MiniBank.Api.Data;

public static class DataExtensions
{
    public static IServiceCollection AddAppDb(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        var POSTGRES_DB = configuration["POSTGRES_DB"];
        var POSTGRES_USER = configuration["POSTGRES_USER"];
        var POSTGRES_PASSWORD = configuration["POSTGRES_PASSWORD"];
        var POSTGRES_PORT = configuration["POSTGRES_PORT"];

        var connectionString =
            $"Host=localhost;Port={POSTGRES_PORT};Database={POSTGRES_DB};Username={POSTGRES_USER};Password={POSTGRES_PASSWORD}";

        Console.WriteLine(connectionString);

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Connection string 'ConnectionStrings:DbConnection' is required."
            );
        }

        services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));
        return services;
    }

    public static async Task MigrateDatabaseAsync(
        this WebApplication app,
        CancellationToken cancellationToken = default
    )
    {
        await using var scope = app.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await dbContext.Database.MigrateAsync(cancellationToken);
    }
}

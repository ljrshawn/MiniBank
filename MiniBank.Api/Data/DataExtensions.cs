using Microsoft.EntityFrameworkCore;

namespace MiniBank.Api.Data;

public static class DataExtensions
{
    public static IServiceCollection AddAppDb(
        this IServiceCollection services,
        IConfiguration configuration,
        bool usePostgres = false
    )
    {
        var connectionName = usePostgres ? "Pgsql" : "Sqlite";
        var connectionString = configuration.GetConnectionString(connectionName);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"Connection string 'ConnectionStrings:{connectionName}' is required."
            );
        }

        if (usePostgres)
        {
            services.AddDbContext<PostgresAppDbContext>(options =>
                options.UseNpgsql(connectionString)
            );
            services.AddScoped<AppDbContext>(provider =>
                provider.GetRequiredService<PostgresAppDbContext>()
            );
            return services;
        }

        services.AddDbContext<AppDbContext>(options =>
            options
                .UseSqlite(connectionString)
                .UseSeeding((context, _) => LegacyPasswordUpgrade.Run(context))
                .UseAsyncSeeding(
                    (context, _, cancellationToken) =>
                        LegacyPasswordUpgrade.RunAsync(context, cancellationToken)
                )
        );

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

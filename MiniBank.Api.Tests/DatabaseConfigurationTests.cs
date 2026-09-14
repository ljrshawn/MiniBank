using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MiniBank.Api.Data;

namespace MiniBank.Api.Tests;

public sealed class DatabaseConfigurationTests
{
    [Theory]
    [InlineData(false, "Sqlite")]
    [InlineData(true, "Pgsql")]
    public void Missing_selected_connection_string_identifies_the_setting(
        bool usePostgres,
        string connectionName
    )
    {
        var configuration = new ConfigurationBuilder().Build();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            new ServiceCollection().AddAppDb(configuration, usePostgres)
        );

        Assert.Contains($"ConnectionStrings:{connectionName}", exception.Message);
    }

    [Theory]
    [InlineData(false, "Microsoft.EntityFrameworkCore.Sqlite", "TEXT", "NOCASE")]
    [InlineData(true, "Npgsql.EntityFrameworkCore.PostgreSQL", "uuid", "minibank_email_nocase")]
    public void Selected_provider_has_its_own_current_model_and_migrations(
        bool usePostgres,
        string providerName,
        string expectedColumnType,
        string expectedCollation
    )
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Sqlite"] = "Data Source=:memory:",
                ["ConnectionStrings:Pgsql"] = "Host=localhost;Database=minibank;Username=minibank",
            })
            .Build();
        using var services = new ServiceCollection()
            .AddLogging()
            .AddAppDb(configuration, usePostgres)
            .BuildServiceProvider();
        using var scope = services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        Assert.Equal(providerName, database.Database.ProviderName);
        Assert.False(database.Database.HasPendingModelChanges());
        var schema = database.Database.GenerateCreateScript();
        Assert.Contains(expectedColumnType, schema);
        Assert.Contains(expectedCollation, schema);

        var migrations = database.Database.GetMigrations().ToArray();
        Assert.NotEmpty(migrations);
        if (usePostgres)
        {
            Assert.Same(database, scope.ServiceProvider.GetRequiredService<PostgresAppDbContext>());
            Assert.EndsWith("_InitialPostgres", Assert.Single(migrations));
            var script = database.GetService<IMigrator>().GenerateScript();
            Assert.Contains("CREATE COLLATION", script);
            Assert.DoesNotContain("NOCASE", script);
            Assert.DoesNotContain("legacy:", script);
        }
        else
        {
            Assert.Contains("20260905015118_InitialCreate", migrations);
            Assert.DoesNotContain(migrations, id => id.EndsWith("_InitialPostgres"));
        }
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MiniBank.Api.Data;

namespace MiniBank.Api.Tests;

public sealed class DatabaseConfigurationTests
{
    [Fact]
    public void Missing_connection_string_identifies_the_setting()
    {
        var configuration = new ConfigurationBuilder().Build();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            new ServiceCollection().AddAppDb(configuration)
        );

        Assert.Contains("ConnectionStrings:DbConnection", exception.Message);
    }

    [Fact]
    public void Postgres_model_matches_migrations_and_includes_identity_constraints()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DbConnection"] = "Host=localhost;Database=minibank;Username=minibank",
            })
            .Build();
        using var services = new ServiceCollection()
            .AddLogging()
            .AddAppDb(configuration)
            .BuildServiceProvider();
        using var scope = services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        Assert.Equal("Npgsql.EntityFrameworkCore.PostgreSQL", database.Database.ProviderName);
        Assert.False(database.Database.HasPendingModelChanges());
        var schema = database.Database.GenerateCreateScript();
        Assert.Contains("uuid", schema);
        Assert.Contains("minibank_email_nocase", schema);
        Assert.Contains("AspNetUserClaims", schema);
        Assert.Contains("CREATE UNIQUE INDEX \"EmailIndex\"", schema);
        Assert.Contains("CREATE UNIQUE INDEX \"UserNameIndex\"", schema);
        Assert.Contains("CREATE UNIQUE INDEX \"IX_Customers_Email\"", schema);

        var script = database.GetService<IMigrator>().GenerateScript();
        Assert.Contains("CREATE COLLATION", script);
        Assert.DoesNotContain("NOCASE", script);
        Assert.Contains(database.Database.GetMigrations(), id => id.EndsWith("_ConfigureIdentityStorage"));
    }
}

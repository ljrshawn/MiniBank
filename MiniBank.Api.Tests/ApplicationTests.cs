using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;

namespace MiniBank.Api.Tests;

public sealed class ApplicationTests
{
    [Fact]
    public async Task Development_initializes_database_and_exposes_documented_endpoints()
    {
        await using var factory = new MiniBankApiFactory("Development");
        using var client = factory.CreateApiClient();
        Assert.Empty((await client.GetFromJsonAsync<JsonArray>("/api/customers"))!);

        var document = await client.GetFromJsonAsync<JsonObject>("/openapi/v1.json");
        var paths = document!["paths"]!.AsObject();
        Assert.All(paths, entry => Assert.StartsWith("/api/", entry.Key));
        Assert.Contains(paths, entry => entry.Key.TrimEnd('/') == "/api/customers");
        Assert.Contains(paths, entry => entry.Key == "/api/customers/{id}");
        var byId = paths["/api/customers/{id}"]!;
        Assert.NotNull(byId["put"]!["responses"]!["409"]);
        Assert.NotNull(byId["delete"]!["responses"]!["404"]);

        var createAccount = paths["/api/accounts/customers/{customerId}"]!["post"]!;
        Assert.NotNull(createAccount["responses"]!["201"]);
        Assert.NotNull(createAccount["responses"]!["400"]);
        Assert.NotNull(createAccount["responses"]!["404"]);
        Assert.NotNull(createAccount["responses"]!["503"]);
        Assert.NotNull(paths["/api/accounts/{id}"]!["get"]!["responses"]!["200"]);
        foreach (var operation in new[] { "deposit", "withdraw" })
        {
            var responses = paths[$"/api/accounts/{{id}}/{operation}"]!["post"]!["responses"]!;
            Assert.NotNull(responses["200"]);
            Assert.NotNull(responses["400"]);
            Assert.NotNull(responses["404"]);
            Assert.NotNull(responses["409"]);
        }
        var history = paths["/api/accounts/{id}/transactions"]!["get"]!["responses"]!;
        Assert.NotNull(history["200"]);
        Assert.NotNull(history["404"]);
    }

    [Fact]
    public async Task Production_does_not_migrate_automatically_or_expose_exception_details()
    {
        await using var factory = new MiniBankApiFactory("Production");
        using var client = factory.CreateApiClient();
        await factory.InDatabaseAsync(async database =>
        {
            Assert.Empty(await database.Database.GetAppliedMigrationsAsync());
        });

        using var response = await client.GetAsync("/api/customers");
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<JsonObject>();
        Assert.Equal(500, problem!["status"]!.GetValue<int>());
        Assert.NotNull(problem["traceId"]);
        Assert.Null(problem["exception"]);
        Assert.DoesNotContain("Sqlite", problem.ToJsonString(), StringComparison.OrdinalIgnoreCase);

        using var openApi = await client.GetAsync("/openapi/v1.json");
        Assert.Equal(HttpStatusCode.NotFound, openApi.StatusCode);
    }
}

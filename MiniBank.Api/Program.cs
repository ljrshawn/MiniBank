using MiniBank.Api.Data;
using MiniBank.Api.Features.Auth;
using MiniBank.Api.Infrastructure;
using MiniBank.Api.Infrastructure.Http;

var migrateDatabase = args.Contains("--migrate-database", StringComparer.Ordinal);

var builder = WebApplication.CreateBuilder(
    args.Where(argument => argument is not "--migrate-database").ToArray()
);

builder.Services.AddAppDb(builder.Configuration);
builder.Services.AddAuthServices(builder.Configuration);
builder.Services.AddOpenApi();
builder.Services.AddApiHttp();
builder.Services.AddApiServices();

var app = builder.Build();

if (migrateDatabase)
{
    await app.MigrateDatabaseAsync();
    app.Logger.LogInformation("Database migrations completed.");
    await app.DisposeAsync();
    return;
}

app.UseApiHttp();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    await app.MigrateDatabaseAsync();
}

app.UseHttpsRedirection();

app.MapApiEndpoints();

await app.RunAsync();

// Exposes the entry point to WebApplicationFactory integration tests.
public partial class Program;

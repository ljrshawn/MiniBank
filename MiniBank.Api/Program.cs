using Microsoft.AspNetCore.Identity;
using MiniBank.Api.Data;
using MiniBank.Api.Entities;
using MiniBank.Api.Features.Accounts;
using MiniBank.Api.Features.Customers;
using MiniBank.Api.Features.Transfers;
using MiniBank.Api.Infrastructure.Http;

var migrateDatabase = args.Contains("--migrate-database", StringComparer.Ordinal);
var usePostgres = args.Contains("--pgsql", StringComparer.Ordinal);

var builder = WebApplication.CreateBuilder(
    args.Where(argument => argument is not "--migrate-database" and not "--pgsql").ToArray()
);

builder.Services.AddOpenApi();
builder.Services.AddApiHttp();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<IPasswordHasher<Customer>, PasswordHasher<Customer>>();
builder.Services.AddScoped<CustomerService>();
builder.Services.AddScoped<AccountService>();
builder.Services.AddScoped<TransferService>();
builder.Services.AddAppDb(builder.Configuration, usePostgres);

var app = builder.Build();

if (migrateDatabase)
{
    await app.MigrateDatabaseAsync();
    app.Logger.LogInformation("Database migrations and credential upgrade completed.");
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

var api = app.MapGroup("/api");
api.MapCustomerEndpoints();
api.MapAccountEndpoints();
api.MapTransferEndpoints();

await app.RunAsync();

// Exposes the entry point to WebApplicationFactory integration tests.
public partial class Program;

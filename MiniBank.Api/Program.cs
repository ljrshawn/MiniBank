using System.Diagnostics;
using Microsoft.AspNetCore.Identity;
using MiniBank.Api.Data;
using MiniBank.Api.Entities;
using MiniBank.Api.Features.Customers;

var migrateDatabase = args.Contains("--migrate-database", StringComparer.Ordinal);
var builder = WebApplication.CreateBuilder(
    args.Where(argument => argument != "--migrate-database").ToArray()
);

builder.Services.AddOpenApi();
builder.Services.AddValidation();
builder.Services.AddProblemDetails(options =>
    options.CustomizeProblemDetails = context =>
    {
        context.ProblemDetails.Instance = context.HttpContext.Request.Path;
        context.ProblemDetails.Extensions["traceId"] =
            Activity.Current?.Id ?? context.HttpContext.TraceIdentifier;
    }
);
builder.Services.Configure<RouteHandlerOptions>(options => options.ThrowOnBadRequest = false);
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<IPasswordHasher<Customer>, PasswordHasher<Customer>>();
builder.Services.AddScoped<CustomerService>();
builder.Services.AddAppDb(builder.Configuration);

var app = builder.Build();

if (migrateDatabase)
{
    await app.MigrateDatabaseAsync();
    app.Logger.LogInformation("Database migrations and credential upgrade completed.");
    await app.DisposeAsync();
    return;
}

app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    await app.MigrateDatabaseAsync();
}

app.UseHttpsRedirection();
app.MapCustomerEndpoints();

await app.RunAsync();

// Exposes the entry point to WebApplicationFactory integration tests.
public partial class Program;

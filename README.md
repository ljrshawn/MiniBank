# MiniBank

A customer and account management API with deposits, withdrawals, transfers, and transaction history, built with .NET 10, ASP.NET Core Minimal APIs, EF Core 10, and SQLite or PostgreSQL.

## Run locally

Install the .NET 10 SDK, then run these commands from the repository root:

```sh
dotnet tool restore
dotnet restore
dotnet dev-certs https --trust
dotnet run --project MiniBank.Api --launch-profile https
```

The API listens at `https://localhost:7136`. Development startup applies migrations and upgrades legacy passwords before accepting requests. OpenAPI is available at `/openapi/v1.json`. Use [MiniBank.Api.http](MiniBank.Api/MiniBank.Api.http) to exercise the endpoints; copy the created customer's ID into `@customerId`.

For local HTTP testing, use `--launch-profile http` and set the HTTP file's `@baseUrl` to `http://localhost:5269`.

```sh
dotnet build MiniBank.slnx --configuration Release
dotnet test MiniBank.slnx --configuration Release
```

The SDK selection in `global.json` permits installed stable .NET 10 feature bands. The local EF tool is pinned to the same version as the EF packages.

## Database upgrades

```sh
# Migrate and upgrade credentials, then exit without starting an HTTP server.
dotnet run --project MiniBank.Api --no-launch-profile -- --migrate-database

# The EF tool also performs the credential upgrade.
dotnet ef database update --project MiniBank.Api --context AppDbContext

# Apply PostgreSQL migrations, then exit.
dotnet run --project MiniBank.Api --no-launch-profile -- --pgsql --migrate-database
dotnet ef database update --project MiniBank.Api --context PostgresAppDbContext -- --pgsql
```

Each provider has its own migration history. After changing the shared model, generate migrations for both contexts:

```sh
dotnet ef migrations add YourChange --project MiniBank.Api --context AppDbContext
dotnet ef migrations add YourChange --project MiniBank.Api --context PostgresAppDbContext --output-dir Migrations/Postgres -- --pgsql
```

Switching providers selects a separate database; it does not copy existing SQLite data into PostgreSQL.

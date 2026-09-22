# MiniBank

A customer and account management API with registration, deposits, withdrawals, transfers, and transaction history, built with .NET 10, ASP.NET Core Minimal APIs, Identity, EF Core 10, and PostgreSQL.

## Run locally

Install the .NET 10 SDK and Docker, then run these commands from the repository root:

```sh
dotnet tool restore
dotnet restore
# For a new checkout, copy the example; keep an existing .env file.
cp -n MiniBank.Api/.env.example MiniBank.Api/.env
docker compose -f MiniBank.Api/compose.yaml up -d
# Generate a local signing key and store it outside the repository.
dotnet user-secrets set "Jwt:Key" "$(openssl rand -base64 32)" --project MiniBank.Api
dotnet dev-certs https --trust
dotnet run --project MiniBank.Api --launch-profile https
```

The default `ConnectionStrings:DbConnection` matches `.env.example`. If using different database credentials, set that connection string through user secrets or `ConnectionStrings__DbConnection`. JWT issuer, audience, and lifetime are in `appsettings.json`; the signing key must contain at least 32 UTF-8 bytes and can also be supplied as `Jwt__Key`.

The API listens at `https://localhost:7136`. Development startup applies migrations. OpenAPI is available at `/openapi/v1.json`. Use [MiniBank.Api.http](MiniBank.Api/MiniBank.Api.http) to exercise the endpoints; copy the created customer's ID into `@customerId`.

For local HTTP testing, use `--launch-profile http` and set the HTTP file's `@baseUrl` to `http://localhost:5269`.

## Build and test

```sh
dotnet build MiniBank.slnx --configuration Release
dotnet test MiniBank.slnx --configuration Release
```

Integration tests use PostgreSQL. By default they connect to the local server using the credentials in `.env.example`. For another server, set `MINIBANK_TEST_CONNECTION_STRING` to an Npgsql connection string. The test account needs permission to create databases. Each test factory creates and removes its own `minibank_test_*` database; the application's database is not used.

The SDK selection in `global.json` permits installed stable .NET 10 feature bands. The local EF tool is pinned to the same version as the EF packages.

## Database migrations

```sh
# Apply migrations, then exit without starting an HTTP server.
dotnet run --project MiniBank.Api --no-launch-profile -- --migrate-database

# Alternatively, use the EF tool.
dotnet ef database update --project MiniBank.Api --context AppDbContext

# After changing entity mappings, create a migration.
dotnet ef migrations add YourChange --project MiniBank.Api --context AppDbContext
```

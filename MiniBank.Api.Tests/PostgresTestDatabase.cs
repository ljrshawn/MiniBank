using Npgsql;

namespace MiniBank.Api.Tests;

internal sealed class PostgresTestDatabase : IAsyncDisposable
{
    private readonly string _name = $"minibank_test_{Guid.NewGuid():N}";
    private readonly string _adminConnectionString;
    private readonly Lock _lock = new();
    private bool _created;

    internal PostgresTestDatabase()
    {
        var builder = new NpgsqlConnectionStringBuilder(
            Environment.GetEnvironmentVariable("MINIBANK_TEST_CONNECTION_STRING")
                ?? "Host=localhost;Database=postgres;Username=minibank;Password=minibank_dev_password"
        )
        {
            Database = "postgres",
            Pooling = false,
        };
        _adminConnectionString = builder.ConnectionString;
        builder.Database = _name;
        ConnectionString = builder.ConnectionString;
    }

    internal string ConnectionString { get; }

    internal void Create()
    {
        lock (_lock)
        {
            if (_created)
            {
                return;
            }

            using var connection = new NpgsqlConnection(_adminConnectionString);
            connection.Open();
            // The identifier is generated locally and never contains user input.
            using var command = new NpgsqlCommand($"CREATE DATABASE \"{_name}\"", connection);
            command.ExecuteNonQuery();
            _created = true;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (!_created)
        {
            return;
        }

        await using var connection = new NpgsqlConnection(_adminConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            $"DROP DATABASE IF EXISTS \"{_name}\" WITH (FORCE)",
            connection
        );
        await command.ExecuteNonQueryAsync();
        _created = false;
    }
}

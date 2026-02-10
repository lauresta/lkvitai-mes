using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace LKvitai.MES.Infrastructure.Projections;

/// <summary>
/// Validates Marten schema at startup and fails fast when required objects are missing.
/// </summary>
public sealed class SchemaValidationService : IHostedService
{
    private static readonly string[] RequiredTables =
    [
        "warehouse_events.mt_events",
        "warehouse_events.mt_streams",
        "warehouse_events.mt_event_progression",
        "warehouse_events.mt_doc_availablestockview",
        "warehouse_events.mt_doc_locationbalanceview"
    ];

    private readonly IConfiguration _configuration;
    private readonly ILogger<SchemaValidationService> _logger;

    public SchemaValidationService(
        IConfiguration configuration,
        ILogger<SchemaValidationService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var skipValidation = bool.TryParse(_configuration["SkipSchemaValidation"], out var parsed) && parsed;
        if (skipValidation)
        {
            _logger.LogWarning("Schema validation skipped because SkipSchemaValidation=true");
            return;
        }

        var connectionString = _configuration.GetConnectionString("WarehouseDb")
            ?? throw new InvalidOperationException("WarehouseDb connection string not found");

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        if (!await SchemaExistsAsync(connection, "warehouse_events", cancellationToken))
        {
            var message = "Schema validation failed: schema 'warehouse_events' does not exist.";
            _logger.LogCritical(message);
            throw new InvalidOperationException(message);
        }

        foreach (var table in RequiredTables)
        {
            var parts = table.Split('.', 2, StringSplitOptions.RemoveEmptyEntries);
            var schema = parts[0];
            var name = parts[1];

            if (await TableExistsAsync(connection, schema, name, cancellationToken))
            {
                continue;
            }

            var message = $"Schema validation failed: required table '{table}' does not exist.";
            _logger.LogCritical(message);
            throw new InvalidOperationException(message);
        }

        _logger.LogInformation("Schema validation completed successfully.");
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private static async Task<bool> SchemaExistsAsync(
        NpgsqlConnection connection,
        string schema,
        CancellationToken cancellationToken)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT EXISTS (SELECT 1 FROM pg_namespace WHERE nspname = @schema)";
        cmd.Parameters.AddWithValue("schema", schema);
        return (bool?)await cmd.ExecuteScalarAsync(cancellationToken) ?? false;
    }

    private static async Task<bool> TableExistsAsync(
        NpgsqlConnection connection,
        string schema,
        string table,
        CancellationToken cancellationToken)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            SELECT EXISTS (
                SELECT 1
                FROM information_schema.tables
                WHERE table_schema = @schema
                  AND table_name = @table)";
        cmd.Parameters.AddWithValue("schema", schema);
        cmd.Parameters.AddWithValue("table", table);
        return (bool?)await cmd.ExecuteScalarAsync(cancellationToken) ?? false;
    }
}

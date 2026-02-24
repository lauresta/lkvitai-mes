using LKvitai.MES.Modules.Warehouse.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace LKvitai.MES.Modules.Warehouse.Api.Services;

public interface ISchemaDriftHealthService
{
    Task<SchemaDriftHealthResult> GetHealthAsync(CancellationToken cancellationToken = default);
}

public sealed record SchemaDriftHealthResult(
    string Status,
    IReadOnlyList<string> PendingMigrations,
    IReadOnlyList<string> MissingTables,
    string Message);

public sealed class SchemaDriftHealthService : ISchemaDriftHealthService
{
    private static readonly string[] RequiredTables =
    [
        "public.backup_executions",
        "public.retention_policies",
        "public.retention_executions",
        "public.gdpr_erasure_requests",
        "public.dr_drills"
    ];

    private readonly WarehouseDbContext _dbContext;
    private readonly ILogger<SchemaDriftHealthService> _logger;

    public SchemaDriftHealthService(WarehouseDbContext dbContext, ILogger<SchemaDriftHealthService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<SchemaDriftHealthResult> GetHealthAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var pendingMigrations = (await _dbContext.Database.GetPendingMigrationsAsync(cancellationToken)).ToArray();
            var missingTables = await FindMissingTablesAsync(cancellationToken);

            if (pendingMigrations.Length == 0 && missingTables.Count == 0)
            {
                return new SchemaDriftHealthResult("Healthy", pendingMigrations, missingTables, "Schema is synchronized.");
            }

            var parts = new List<string>();
            if (pendingMigrations.Length > 0)
            {
                parts.Add($"Pending migrations: {string.Join(", ", pendingMigrations)}");
            }

            if (missingTables.Count > 0)
            {
                parts.Add($"Missing tables: {string.Join(", ", missingTables)}");
            }

            return new SchemaDriftHealthResult("Unhealthy", pendingMigrations, missingTables, string.Join(" | ", parts));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Schema drift health check failed.");
            return new SchemaDriftHealthResult(
                "Unhealthy",
                Array.Empty<string>(),
                Array.Empty<string>(),
                $"Schema check failed: {ex.Message}");
        }
    }

    private async Task<IReadOnlyList<string>> FindMissingTablesAsync(CancellationToken cancellationToken)
    {
        var connection = (NpgsqlConnection)_dbContext.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }

        var missing = new List<string>();
        foreach (var table in RequiredTables)
        {
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = "select to_regclass(@name)::text";
            cmd.Parameters.AddWithValue("name", table);

            var result = await cmd.ExecuteScalarAsync(cancellationToken);
            if (result is null || result is DBNull)
            {
                missing.Add(table);
            }
        }

        return missing;
    }
}

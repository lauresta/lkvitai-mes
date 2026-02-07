using LKvitai.MES.Application.Commands;
using LKvitai.MES.Application.Projections;
using LKvitai.MES.Contracts.Events;
using LKvitai.MES.Contracts.ReadModels;
using LKvitai.MES.SharedKernel;
using Marten;
using Microsoft.Extensions.Logging;
using Npgsql;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;

namespace LKvitai.MES.Infrastructure.Projections;

/// <summary>
/// Projection rebuild service implementation
/// [MITIGATION V-5] Implements deterministic projection rebuild with shadow table verification
/// 
/// Rebuild Contract per design document:
/// - Rule A: Stream-ordered replay (by sequence number, not timestamp)
/// - Rule B: Self-contained event data (no external queries)
/// - Rule C: Rebuild verification gate (shadow table + checksum + atomic swap)
/// </summary>
public class ProjectionRebuildService : IProjectionRebuildService
{
    private readonly IDocumentStore _documentStore;
    private readonly ILogger<ProjectionRebuildService> _logger;
    
    public ProjectionRebuildService(
        IDocumentStore documentStore,
        ILogger<ProjectionRebuildService> logger)
    {
        _documentStore = documentStore;
        _logger = logger;
    }
    
    public async Task<Result<ProjectionRebuildReport>> RebuildProjectionAsync(
        string projectionName,
        bool verify = true,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        
        _logger.LogInformation(
            "Starting projection rebuild for {ProjectionName} with verify={Verify}",
            projectionName,
            verify);
        
        try
        {
            // Only LocationBalance is implemented for Package C
            if (projectionName != "LocationBalance")
            {
                return Result<ProjectionRebuildReport>.Fail(
                    $"Projection '{projectionName}' rebuild not implemented");
            }
            
            // Step 1: Create shadow table
            await CreateShadowTableAsync(cancellationToken);
            
            // Step 2: Replay events to shadow table in stream order (V-5 Rule A)
            var eventsProcessed = await ReplayEventsToShadowAsync(cancellationToken);
            
            // Step 3: Compute checksums
            var productionChecksum = await ComputeChecksumAsync("mt_doc_locationbalanceview", cancellationToken);
            var shadowChecksum = await ComputeChecksumAsync("mt_doc_locationbalanceview_shadow", cancellationToken);
            
            var checksumMatch = productionChecksum == shadowChecksum;
            
            _logger.LogInformation(
                "Checksums - Production: {ProductionChecksum}, Shadow: {ShadowChecksum}, Match: {Match}",
                productionChecksum,
                shadowChecksum,
                checksumMatch);
            
            // Step 4: Verify and optionally swap
            var swapped = false;
            if (verify)
            {
                if (!checksumMatch)
                {
                    _logger.LogWarning(
                        "Checksum mismatch for projection {ProjectionName}. " +
                        "Production: {ProductionChecksum}, Shadow: {ShadowChecksum}. " +
                        "Swap aborted.",
                        projectionName,
                        productionChecksum,
                        shadowChecksum);
                    
                    // Do NOT swap - return failure
                    stopwatch.Stop();
                    return Result<ProjectionRebuildReport>.Fail(
                        $"Checksum verification failed. " +
                        $"Production: {productionChecksum}, Shadow: {shadowChecksum}");
                }
                
                // Checksums match - proceed with swap
                await SwapTablesAsync(cancellationToken);
                swapped = true;
                
                _logger.LogInformation(
                    "Projection {ProjectionName} rebuilt and swapped successfully",
                    projectionName);
            }
            else
            {
                _logger.LogInformation(
                    "Projection {ProjectionName} rebuilt to shadow table. " +
                    "Verify=false, so no swap performed.",
                    projectionName);
            }
            
            stopwatch.Stop();
            
            var report = new ProjectionRebuildReport
            {
                ProjectionName = projectionName,
                EventsProcessed = eventsProcessed,
                ProductionChecksum = productionChecksum,
                ShadowChecksum = shadowChecksum,
                ChecksumMatch = checksumMatch,
                Swapped = swapped,
                Duration = stopwatch.Elapsed
            };
            
            return Result<ProjectionRebuildReport>.Ok(report);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error rebuilding projection {ProjectionName}", projectionName);
            stopwatch.Stop();
            return Result<ProjectionRebuildReport>.Fail($"Rebuild failed: {ex.Message}");
        }
    }
    
    public async Task<ProjectionDiffReport> GenerateDiffReportAsync(
        string projectionName,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Generating diff report for {ProjectionName}",
            projectionName);
        
        // V-5: Minimal diff report implementation for Package C
        // Full implementation can be added later
        
        await Task.CompletedTask;
        return new ProjectionDiffReport
        {
            ProjectionName = projectionName,
            RowsOnlyInProduction = 0,
            RowsOnlyInShadow = 0,
            RowsWithDifferences = 0,
            SampleDifferences = new List<string> { "Diff report not yet implemented" }
        };
    }
    
    private async Task CreateShadowTableAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Creating shadow table for LocationBalanceView");
        
        await using var session = _documentStore.LightweightSession();
        var conn = (NpgsqlConnection)session.Connection!;
        
        // Drop shadow table if exists
        await using var dropCmd = conn.CreateCommand();
        dropCmd.CommandText = "DROP TABLE IF EXISTS mt_doc_locationbalanceview_shadow CASCADE";
        await dropCmd.ExecuteNonQueryAsync(cancellationToken);
        
        // Create shadow table (same schema as production)
        await using var createCmd = conn.CreateCommand();
        createCmd.CommandText = @"
            CREATE TABLE mt_doc_locationbalanceview_shadow (LIKE mt_doc_locationbalanceview INCLUDING ALL)";
        await createCmd.ExecuteNonQueryAsync(cancellationToken);
        
        _logger.LogInformation("Shadow table created successfully");
    }
    
    private async Task<int> ReplayEventsToShadowAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Replaying events to shadow table in stream order (V-5 Rule A)");
        
        await using var session = _documentStore.QuerySession();
        
        // V-5 Rule A: Order by sequence number (NOT timestamp)
        var events = await session.Events
            .QueryAllRawEvents()
            .Where(e => e.EventTypeName == typeof(StockMovedEvent).FullName)
            .OrderBy(e => e.Sequence)  // CRITICAL: Stream order, not timestamp
            .ToListAsync(cancellationToken);
        
        _logger.LogInformation("Found {EventCount} StockMoved events to replay", events.Count);
        
        var balances = new Dictionary<string, LocationBalanceView>();
        
        foreach (var rawEvent in events)
        {
            var evt = rawEvent.Data as StockMovedEvent;
            if (evt == null) continue;
            
            // V-5 Rule B: Use only self-contained event data
            // Extract warehouseId from stream key (string-based stream ID)
            var streamKey = rawEvent.StreamKey ?? throw new InvalidOperationException("StreamKey is null");
            var streamId = Domain.StockLedgerStreamId.Parse(streamKey);
            var warehouseId = streamId.WarehouseId;
            
            // Create identities for FROM and TO locations
            var fromKey = $"{warehouseId}:{evt.FromLocation}:{evt.SKU}";
            var toKey = $"{warehouseId}:{evt.ToLocation}:{evt.SKU}";
            
            // Apply to FROM location (decrease)
            if (!balances.ContainsKey(fromKey))
            {
                balances[fromKey] = new LocationBalanceView
                {
                    Id = fromKey,
                    WarehouseId = warehouseId,
                    Location = evt.FromLocation,
                    SKU = evt.SKU,
                    Quantity = 0,
                    LastUpdated = evt.Timestamp
                };
            }
            balances[fromKey].Quantity -= evt.Quantity;
            balances[fromKey].LastUpdated = evt.Timestamp;
            
            // Apply to TO location (increase)
            if (!balances.ContainsKey(toKey))
            {
                balances[toKey] = new LocationBalanceView
                {
                    Id = toKey,
                    WarehouseId = warehouseId,
                    Location = evt.ToLocation,
                    SKU = evt.SKU,
                    Quantity = 0,
                    LastUpdated = evt.Timestamp
                };
            }
            balances[toKey].Quantity += evt.Quantity;
            balances[toKey].LastUpdated = evt.Timestamp;
        }
        
        // Insert all balances into shadow table
        await using var insertSession = _documentStore.LightweightSession();
        var conn = (NpgsqlConnection)insertSession.Connection!;
        
        foreach (var balance in balances.Values)
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO mt_doc_locationbalanceview_shadow (id, data)
                VALUES (@id, @data::jsonb)";
            cmd.Parameters.AddWithValue("id", balance.Id);
            cmd.Parameters.AddWithValue("data", System.Text.Json.JsonSerializer.Serialize(balance));
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }
        
        _logger.LogInformation(
            "Replayed {EventCount} events resulting in {BalanceCount} balance records",
            events.Count,
            balances.Count);
        
        return events.Count;
    }
    
    private async Task<string> ComputeChecksumAsync(
        string tableName,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Computing checksum for {TableName}", tableName);
        
        await using var session = _documentStore.QuerySession();
        var conn = (NpgsqlConnection)session.Connection!;
        
        // Compute MD5 checksum of all rows (ordered by id for determinism)
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $@"
            SELECT COALESCE(
                MD5(STRING_AGG(id || data::text, '' ORDER BY id)),
                'empty'
            ) as checksum
            FROM {tableName}";
        
        var checksum = (string?)await cmd.ExecuteScalarAsync(cancellationToken) ?? "empty";
        
        _logger.LogInformation("Checksum for {TableName}: {Checksum}", tableName, checksum);
        
        return checksum;
    }
    
    private async Task SwapTablesAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Swapping shadow table to production (atomic swap)");
        
        await using var session = _documentStore.LightweightSession();
        var conn = (NpgsqlConnection)session.Connection!;
        
        // Atomic swap: rename tables in transaction
        await using var transaction = await conn.BeginTransactionAsync(cancellationToken);
        
        try
        {
            // Rename production to _old
            await using var renameOldCmd = conn.CreateCommand();
            renameOldCmd.Transaction = transaction;
            renameOldCmd.CommandText = 
                "ALTER TABLE mt_doc_locationbalanceview RENAME TO mt_doc_locationbalanceview_old";
            await renameOldCmd.ExecuteNonQueryAsync(cancellationToken);
            
            // Rename shadow to production
            await using var renameShadowCmd = conn.CreateCommand();
            renameShadowCmd.Transaction = transaction;
            renameShadowCmd.CommandText = 
                "ALTER TABLE mt_doc_locationbalanceview_shadow RENAME TO mt_doc_locationbalanceview";
            await renameShadowCmd.ExecuteNonQueryAsync(cancellationToken);
            
            await transaction.CommitAsync(cancellationToken);
            
            _logger.LogInformation("Tables swapped successfully");
            
            // Drop old table (optional - could keep for rollback)
            await using var dropCmd = conn.CreateCommand();
            dropCmd.CommandText = "DROP TABLE IF EXISTS mt_doc_locationbalanceview_old CASCADE";
            await dropCmd.ExecuteNonQueryAsync(cancellationToken);
            
            _logger.LogInformation("Old table dropped");
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

using DotNet.Testcontainers.Builders;
using FluentAssertions;
using LKvitai.MES.Contracts.Events;
using LKvitai.MES.Contracts.ReadModels;
using LKvitai.MES.Domain;
using LKvitai.MES.Infrastructure.Projections;
using LKvitai.MES.Projections;
using Marten;
using Marten.Events.Projections;
using Microsoft.Extensions.Logging.Abstractions;
using Testcontainers.PostgreSql;
using Xunit;

namespace LKvitai.MES.Tests.Integration;

/// <summary>
/// Integration tests for LocationBalance projection rebuild (V-5 mitigation)
/// Validates V-5 rebuild contract: shadow table + verification + swap
/// 
/// These tests are opt-in. Set <c>TESTCONTAINERS_ENABLED=1</c> to run them.
/// </summary>
public class LocationBalanceRebuildTests : IAsyncLifetime
{
    private PostgreSqlContainer? _postgres;
    private IDocumentStore? _documentStore;
    private string _connectionString = string.Empty;
    
    public async Task InitializeAsync()
    {
        // Skip if Docker unavailable (per Package B gating)
        if (!DockerRequirement.IsEnabled)
            return;
        
        _postgres = new PostgreSqlBuilder()
            .WithImage("postgres:16-alpine")
            .Build();
        
        await _postgres.StartAsync();
        _connectionString = _postgres.GetConnectionString();
        
        _documentStore = DocumentStore.For(opts =>
        {
            opts.Connection(_connectionString);
            opts.DatabaseSchemaName = "test_locationbalance";
            opts.Events.StreamIdentity = Marten.Events.StreamIdentity.AsString;
            
            // Register LocationBalance projection as Async
            opts.Projections.Add<LocationBalanceProjection>(ProjectionLifecycle.Async);
        });
        
        // Ensure schema is created
        await _documentStore.Storage.ApplyAllConfiguredChangesToDatabaseAsync();
    }
    
    public async Task DisposeAsync()
    {
        _documentStore?.Dispose();
        if (_postgres is not null)
            await _postgres.DisposeAsync();
    }
    
    /// <summary>
    /// Verifies that rebuilding the projection creates correct balances from events
    /// and that shadow table + verification + swap works end-to-end
    /// </summary>
    [SkippableFact]
    public async Task RebuildProjection_CreatesBalancesFromEvents()
    {
        // Gate on Docker (per Package B)
        DockerRequirement.EnsureEnabled();
        
        // Arrange: Insert stock movement events
        await using var session = _documentStore!.LightweightSession();
        
        var warehouseId = "MAIN";
        var location = "BIN-001";
        var sku = "SKU933";
        var streamId = StockLedgerStreamId.For(warehouseId, location, sku);
        
        var events = new[]
        {
            new StockMovedEvent
            {
                MovementId = Guid.NewGuid(),
                SKU = sku,
                Quantity = 100m,
                FromLocation = "SUPPLIER",
                ToLocation = location,
                MovementType = "RECEIPT",
                OperatorId = Guid.NewGuid(),
                Timestamp = DateTime.UtcNow
            },
            new StockMovedEvent
            {
                MovementId = Guid.NewGuid(),
                SKU = sku,
                Quantity = 30m,
                FromLocation = location,
                ToLocation = "PRODUCTION",
                MovementType = "PICK",
                OperatorId = Guid.NewGuid(),
                Timestamp = DateTime.UtcNow.AddMinutes(1)
            }
        };
        
        session.Events.Append(streamId, events);
        await session.SaveChangesAsync();
        
        // Process async projection (live)
        using var daemon = await _documentStore.BuildProjectionDaemonAsync();
        await daemon.StartAllAsync();
        await daemon.WaitForNonStaleData(TimeSpan.FromSeconds(10));
        await daemon.StopAllAsync();
        
        // Act: Rebuild projection using shadow table approach
        var logger = NullLogger<ProjectionRebuildService>.Instance;
        var rebuildService = new ProjectionRebuildService(_documentStore, logger);
        var result = await rebuildService.RebuildProjectionAsync(
            "LocationBalance",
            verify: true);
        
        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.EventsProcessed.Should().Be(2);
        result.Value.ChecksumMatch.Should().BeTrue();
        result.Value.Swapped.Should().BeTrue();
        
        // Verify final balance matches expected
        await using var querySession = _documentStore.QuerySession();
        var key = $"{warehouseId}:{location}:{sku}";
        var balance = await querySession.LoadAsync<LocationBalanceView>(key);
        
        balance.Should().NotBeNull();
        balance!.Quantity.Should().Be(70m, "100 received - 30 picked = 70");
    }
    
    /// <summary>
    /// Validates V-5 Rule A: Rebuild must use stream order (sequence), not timestamp order
    /// </summary>
    [SkippableFact]
    public async Task Rebuild_UsesSequenceNotTimestamp()
    {
        // Gate on Docker (per Package B)
        DockerRequirement.EnsureEnabled();
        
        // Arrange: Insert events with out-of-order timestamps
        await using var session = _documentStore!.LightweightSession();
        
        var warehouseId = "MAIN";
        var location = "BIN-002";
        var sku = "SKU999";
        var streamId = StockLedgerStreamId.For(warehouseId, location, sku);
        
        // Event 1: Later timestamp but earlier sequence
        var event1 = new StockMovedEvent
        {
            MovementId = Guid.NewGuid(),
            SKU = sku,
            Quantity = 50m,
            FromLocation = "SUPPLIER",
            ToLocation = location,
            MovementType = "RECEIPT",
            OperatorId = Guid.NewGuid(),
            Timestamp = DateTime.UtcNow.AddHours(1)  // Later timestamp
        };
        
        // Event 2: Earlier timestamp but later sequence
        var event2 = new StockMovedEvent
        {
            MovementId = Guid.NewGuid(),
            SKU = sku,
            Quantity = 20m,
            FromLocation = location,
            ToLocation = "PRODUCTION",
            MovementType = "PICK",
            OperatorId = Guid.NewGuid(),
            Timestamp = DateTime.UtcNow  // Earlier timestamp
        };
        
        session.Events.Append(streamId, event1, event2);
        await session.SaveChangesAsync();
        
        // Process async projection (live)
        using var daemon = await _documentStore.BuildProjectionDaemonAsync();
        await daemon.StartAllAsync();
        await daemon.WaitForNonStaleData(TimeSpan.FromSeconds(10));
        await daemon.StopAllAsync();
        
        // Act: Rebuild projection
        var logger = NullLogger<ProjectionRebuildService>.Instance;
        var rebuildService = new ProjectionRebuildService(_documentStore, logger);
        var result = await rebuildService.RebuildProjectionAsync(
            "LocationBalance",
            verify: true);
        
        // Assert: Rebuild should match live projection (stream order, not timestamp)
        result.IsSuccess.Should().BeTrue();
        result.Value.ChecksumMatch.Should().BeTrue("rebuild must use stream order");
        
        // Verify balance (50 received - 20 picked = 30)
        await using var querySession = _documentStore.QuerySession();
        var key = $"{warehouseId}:{location}:{sku}";
        var balance = await querySession.LoadAsync<LocationBalanceView>(key);
        
        balance.Should().NotBeNull();
        balance!.Quantity.Should().Be(30m);
    }
    
    /// <summary>
    /// Validates V-5 Rule C: Verification gate must prevent swap when checksum mismatches
    /// </summary>
    [SkippableFact]
    public async Task Rebuild_WithVerification_DetectsMismatch()
    {
        // Gate on Docker (per Package B)
        DockerRequirement.EnsureEnabled();
        
        // Arrange: Insert minimal events
        await using var session = _documentStore!.LightweightSession();
        
        var warehouseId = "MAIN";
        var location = "BIN-003";
        var sku = "SKU777";
        var streamId = StockLedgerStreamId.For(warehouseId, location, sku);
        
        var event1 = new StockMovedEvent
        {
            MovementId = Guid.NewGuid(),
            SKU = sku,
            Quantity = 100m,
            FromLocation = "SUPPLIER",
            ToLocation = location,
            MovementType = "RECEIPT",
            OperatorId = Guid.NewGuid(),
            Timestamp = DateTime.UtcNow
        };
        
        session.Events.Append(streamId, event1);
        await session.SaveChangesAsync();
        
        // Process async projection (live)
        using var daemon = await _documentStore.BuildProjectionDaemonAsync();
        await daemon.StartAllAsync();
        await daemon.WaitForNonStaleData(TimeSpan.FromSeconds(10));
        await daemon.StopAllAsync();
        
        // Manually corrupt production table to force mismatch
        await using var corruptSession = _documentStore.LightweightSession();
        var conn = (Npgsql.NpgsqlConnection)corruptSession.Connection!;
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE test_locationbalance.mt_doc_locationbalanceview SET data = jsonb_set(data, '{Quantity}', '999') WHERE id LIKE '%BIN-003%'";
        await cmd.ExecuteNonQueryAsync();
        
        // Act: Rebuild should detect mismatch
        var logger = NullLogger<ProjectionRebuildService>.Instance;
        var rebuildService = new ProjectionRebuildService(_documentStore, logger);
        var result = await rebuildService.RebuildProjectionAsync(
            "LocationBalance",
            verify: true);
        
        // Assert: Rebuild should FAIL due to checksum mismatch
        result.IsSuccess.Should().BeFalse("verification should prevent swap on mismatch");
        result.Error.Should().Contain("Checksum verification failed");
    }
}

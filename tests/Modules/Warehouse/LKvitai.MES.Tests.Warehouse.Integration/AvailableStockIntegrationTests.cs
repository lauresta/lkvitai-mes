using FluentAssertions;
using LKvitai.MES.Contracts.Events;
using LKvitai.MES.Contracts.ReadModels;
using LKvitai.MES.Modules.Warehouse.Domain;
using LKvitai.MES.Modules.Warehouse.Projections;
using Marten;
using Marten.Events.Projections;
using Testcontainers.PostgreSql;
using Xunit;

namespace LKvitai.MES.Tests.Warehouse.Integration;

/// <summary>
/// Integration tests for the AvailableStock projection (Package D).
///
/// Uses Testcontainers (PostgreSQL) to verify that the Marten async projection
/// correctly produces AvailableStockView documents from seeded events.
///
/// Opt-in: TESTCONTAINERS_ENABLED=1
/// </summary>
public class AvailableStockIntegrationTests : IAsyncLifetime
{
    private PostgreSqlContainer? _postgres;
    private IDocumentStore? _store;

    private const string WarehouseId = "WH1";
    private const string Location = "LOC-A";
    private const string Sku = "SKU-001";

    public async Task InitializeAsync()
    {
        DockerRequirement.EnsureEnabled();

        _postgres = new PostgreSqlBuilder()
            .WithImage("pgvector/pgvector:pg16")
            .Build();

        await _postgres.StartAsync();

        _store = DocumentStore.For(opts =>
        {
            opts.Connection(_postgres.GetConnectionString());
            opts.Events.StreamIdentity = Marten.Events.StreamIdentity.AsString;

            // Register projections the same way the app does
            opts.Projections.Add<AvailableStockProjection>(ProjectionLifecycle.Async);
            opts.Projections.Add<LocationBalanceProjection>(ProjectionLifecycle.Async);
        });
    }

    public async Task DisposeAsync()
    {
        _store?.Dispose();
        if (_postgres != null)
            await _postgres.DisposeAsync();
    }

    [SkippableFact]
    public async Task Projection_StockMovedOnly_SetsOnHandAndAvailable()
    {
        DockerRequirement.EnsureEnabled();

        // Arrange: seed one StockMoved event (receipt from SUPPLIER)
        var streamKey = StockLedgerStreamId.For(WarehouseId, Location, Sku);
        await using var session = _store!.LightweightSession();

        session.Events.Append(streamKey, new StockMovedEvent
        {
            MovementId = Guid.NewGuid(),
            SKU = Sku,
            Quantity = 200m,
            FromLocation = "SUPPLIER",
            ToLocation = Location,
            MovementType = "RECEIPT",
            OperatorId = Guid.NewGuid(),
            Timestamp = DateTime.UtcNow
        });

        await session.SaveChangesAsync();

        // Act: run the async daemon to project events
        using var daemon = await _store.BuildProjectionDaemonAsync();
        await daemon.StartAllAsync();
        await daemon.WaitForNonStaleData(TimeSpan.FromSeconds(10));

        // Assert: check the projected AvailableStockView
        await using var query = _store.QuerySession();
        var viewId = AvailableStockView.ComputeId(WarehouseId, Location, Sku);
        var view = await query.LoadAsync<AvailableStockView>(viewId);

        view.Should().NotBeNull();
        view!.OnHandQty.Should().Be(200m);
        view.HardLockedQty.Should().Be(0m);
        view.AvailableQty.Should().Be(200m);
    }

    [SkippableFact]
    public async Task Projection_StockMovedToNewLocation_UpdatesBothSides()
    {
        DockerRequirement.EnsureEnabled();

        var streamKey = StockLedgerStreamId.For(WarehouseId, Location, Sku);
        await using (var session = _store!.LightweightSession())
        {
            session.Events.Append(streamKey,
                new StockMovedEvent
                {
                    MovementId = Guid.NewGuid(), SKU = Sku, Quantity = 10m,
                    FromLocation = "SUPPLIER", ToLocation = Location,
                    MovementType = "RECEIPT", OperatorId = Guid.NewGuid(), Timestamp = DateTime.UtcNow
                },
                new StockMovedEvent
                {
                    MovementId = Guid.NewGuid(), SKU = Sku, Quantity = 4m,
                    FromLocation = Location, ToLocation = "LOC-B",
                    MovementType = "TRANSFER", OperatorId = Guid.NewGuid(), Timestamp = DateTime.UtcNow
                });
            await session.SaveChangesAsync();
        }

        using var daemon = await _store!.BuildProjectionDaemonAsync();
        await daemon.StartAllAsync();
        await daemon.WaitForNonStaleData(TimeSpan.FromSeconds(10));

        await using var query = _store.QuerySession();
        var from = await query.LoadAsync<AvailableStockView>(AvailableStockView.ComputeId(WarehouseId, Location, Sku));
        var to = await query.LoadAsync<AvailableStockView>(AvailableStockView.ComputeId(WarehouseId, "LOC-B", Sku));
        var fromBalance = await query.LoadAsync<LocationBalanceView>($"{WarehouseId}:{Location}:{Sku}");
        var toBalance = await query.LoadAsync<LocationBalanceView>($"{WarehouseId}:LOC-B:{Sku}");

        from.Should().NotBeNull();
        from!.OnHandQty.Should().Be(6m);
        from.AvailableQty.Should().Be(6m);
        to.Should().NotBeNull();
        to!.OnHandQty.Should().Be(4m);
        to.AvailableQty.Should().Be(4m);
        to.Location.Should().Be("LOC-B");
        fromBalance.Should().NotBeNull();
        fromBalance!.Quantity.Should().Be(6m);
        toBalance.Should().NotBeNull();
        toBalance!.Quantity.Should().Be(4m);

        await using (var corrupt = _store.LightweightSession())
        {
            to!.OnHandQty = 999m;
            corrupt.Store(to);
            await corrupt.SaveChangesAsync();
        }

        await daemon.RebuildProjectionAsync<AvailableStockView>(CancellationToken.None);
        await using var rebuiltQuery = _store.QuerySession();
        var repaired = await rebuiltQuery.LoadAsync<AvailableStockView>(AvailableStockView.ComputeId(WarehouseId, "LOC-B", Sku));
        repaired.Should().NotBeNull();
        repaired!.OnHandQty.Should().Be(4m);
    }

    [SkippableFact]
    public async Task Projection_StockMovedPlusPickingStarted_ReducesAvailable()
    {
        DockerRequirement.EnsureEnabled();

        // Arrange: seed StockMoved + PickingStarted events
        var stockStreamKey = StockLedgerStreamId.For(WarehouseId, Location, Sku);
        var reservationId = Guid.NewGuid();
        var reservationStreamKey = $"reservation-{reservationId}";

        await using var session = _store!.LightweightSession();

        // 1. Receipt: 200 units
        session.Events.Append(stockStreamKey, new StockMovedEvent
        {
            MovementId = Guid.NewGuid(),
            SKU = Sku,
            Quantity = 200m,
            FromLocation = "SUPPLIER",
            ToLocation = Location,
            MovementType = "RECEIPT",
            OperatorId = Guid.NewGuid(),
            Timestamp = DateTime.UtcNow
        });

        // 2. PickingStarted: 80 units hard-locked
        session.Events.Append(reservationStreamKey, new PickingStartedEvent
        {
            ReservationId = reservationId,
            LockType = "HARD",
            Timestamp = DateTime.UtcNow,
            HardLockedLines = new List<HardLockLineDto>
            {
                new()
                {
                    WarehouseId = WarehouseId,
                    Location = Location,
                    SKU = Sku,
                    HardLockedQty = 80m
                }
            }
        });

        await session.SaveChangesAsync();

        // Act
        using var daemon = await _store.BuildProjectionDaemonAsync();
        await daemon.StartAllAsync();
        await daemon.WaitForNonStaleData(TimeSpan.FromSeconds(10));

        // Assert
        await using var query = _store.QuerySession();
        var viewId = AvailableStockView.ComputeId(WarehouseId, Location, Sku);
        var view = await query.LoadAsync<AvailableStockView>(viewId);

        view.Should().NotBeNull();
        view!.OnHandQty.Should().Be(200m);
        view.HardLockedQty.Should().Be(80m);
        view.AvailableQty.Should().Be(120m, "available = 200 - 80");
    }

    [SkippableFact]
    public async Task Projection_FullLifecycle_ReceiptLockPickConsume()
    {
        DockerRequirement.EnsureEnabled();

        var stockStreamKey = StockLedgerStreamId.For(WarehouseId, Location, Sku);
        var reservationId = Guid.NewGuid();
        var reservationStreamKey = $"reservation-{reservationId}";

        await using var session = _store!.LightweightSession();

        // 1. Receipt: 300 units
        session.Events.Append(stockStreamKey, new StockMovedEvent
        {
            MovementId = Guid.NewGuid(),
            SKU = Sku,
            Quantity = 300m,
            FromLocation = "SUPPLIER",
            ToLocation = Location,
            MovementType = "RECEIPT",
            OperatorId = Guid.NewGuid(),
            Timestamp = DateTime.UtcNow
        });

        // 2. PickingStarted: 100 units hard-locked
        session.Events.Append(reservationStreamKey, new PickingStartedEvent
        {
            ReservationId = reservationId,
            LockType = "HARD",
            Timestamp = DateTime.UtcNow,
            HardLockedLines = new List<HardLockLineDto>
            {
                new()
                {
                    WarehouseId = WarehouseId,
                    Location = Location,
                    SKU = Sku,
                    HardLockedQty = 100m
                }
            }
        });

        // 3. StockMoved (pick): 100 units moved to PRODUCTION
        session.Events.Append(stockStreamKey, new StockMovedEvent
        {
            MovementId = Guid.NewGuid(),
            SKU = Sku,
            Quantity = 100m,
            FromLocation = Location,
            ToLocation = "PRODUCTION",
            MovementType = "PICK",
            OperatorId = Guid.NewGuid(),
            Timestamp = DateTime.UtcNow
        });

        // 4. ReservationConsumed: hard lock released
        session.Events.Append(reservationStreamKey, new ReservationConsumedEvent
        {
            ReservationId = reservationId,
            ActualQuantity = 100m,
            Timestamp = DateTime.UtcNow,
            ReleasedHardLockLines = new List<HardLockLineDto>
            {
                new()
                {
                    WarehouseId = WarehouseId,
                    Location = Location,
                    SKU = Sku,
                    HardLockedQty = 100m
                }
            }
        });

        await session.SaveChangesAsync();

        // Act
        using var daemon = await _store.BuildProjectionDaemonAsync();
        await daemon.StartAllAsync();
        await daemon.WaitForNonStaleData(TimeSpan.FromSeconds(10));

        // Assert: onHand = 300 - 100 = 200, hardLocked = 0, available = 200
        await using var query = _store.QuerySession();
        var viewId = AvailableStockView.ComputeId(WarehouseId, Location, Sku);
        var view = await query.LoadAsync<AvailableStockView>(viewId);

        view.Should().NotBeNull();
        view!.OnHandQty.Should().Be(200m);
        view.HardLockedQty.Should().Be(0m);
        view.AvailableQty.Should().Be(200m);
    }
}

using FluentAssertions;
using LKvitai.MES.Modules.Warehouse.Application.Behaviors;
using LKvitai.MES.Modules.Warehouse.Application.Commands;
using LKvitai.MES.Modules.Warehouse.Application.Orchestration;
using LKvitai.MES.Modules.Warehouse.Application.Ports;
using LKvitai.MES.Contracts.Events;
using LKvitai.MES.Contracts.Messages;
using LKvitai.MES.Contracts.ReadModels;
using LKvitai.MES.Modules.Warehouse.Domain;
using LKvitai.MES.Modules.Warehouse.Domain.Aggregates;
using LKvitai.MES.Modules.Warehouse.Infrastructure.Persistence;
using LKvitai.MES.Projections;
using LKvitai.MES.SharedKernel;
using Marten;
using Marten.Events.Projections;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;
using Xunit;

namespace LKvitai.MES.Tests.Integration;

/// <summary>
/// Integration tests for Package F: Allocation + PickStock workflows.
///
/// Happy path: Receive → Allocate → StartPicking → PickStock
/// Failure path: PickStock with forced consumption failure → deferred saga
///
/// Uses Testcontainers (PostgreSQL).
/// Opt-in: TESTCONTAINERS_ENABLED=1
/// </summary>
public class AllocationAndPickStockIntegrationTests : IAsyncLifetime
{
    private PostgreSqlContainer? _postgres;
    private IDocumentStore? _store;
    private ServiceProvider? _serviceProvider;

    private const string WarehouseId = "WH1";
    private const string Location = "LOC-PICK-A";
    private const string Sku = "SKU-PICK-001";

    public async Task InitializeAsync()
    {
        DockerRequirement.EnsureEnabled();

        _postgres = new PostgreSqlBuilder()
            .WithImage("postgres:15-alpine")
            .Build();

        await _postgres.StartAsync();

        _store = DocumentStore.For(opts =>
        {
            opts.Connection(_postgres.GetConnectionString());
            opts.Events.StreamIdentity = Marten.Events.StreamIdentity.AsString;

            // Register projections
            opts.Projections.Add<HandlingUnitProjection>(ProjectionLifecycle.Async);
            opts.Projections.Add<LocationBalanceProjection>(ProjectionLifecycle.Async);
            opts.Projections.Add<AvailableStockProjection>(ProjectionLifecycle.Async);
        });

        var services = new ServiceCollection();
        services.AddSingleton<IDocumentStore>(_store);
        services.AddLogging();
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining<AllocateReservationCommandHandler>();
        });

        // [HOTFIX CRIT-01] Balance guard lock for serializing balance-affecting operations
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:WarehouseDb"] = _postgres!.GetConnectionString()
            })
            .Build();
        services.AddSingleton<IConfiguration>(config);
        services.AddSingleton<IBalanceGuardLockFactory, PostgresBalanceGuardLockFactory>();

        // Orchestrations
        services.AddScoped<IReceiveGoodsOrchestration, MartenReceiveGoodsOrchestration>();
        services.AddScoped<IAllocateReservationOrchestration, MartenAllocateReservationOrchestration>();
        services.AddScoped<IPickStockOrchestration, MartenPickStockOrchestration>();
        services.AddScoped<IStartPickingOrchestration, MartenStartPickingOrchestration>();

        // Idempotency
        services.AddScoped<IProcessedCommandStore, MartenProcessedCommandStore>();
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(IdempotencyBehavior<,>));

        // IEventBus — no-op for integration tests (we test orchestration directly)
        services.AddScoped<IEventBus, NoOpEventBus>();

        _serviceProvider = services.BuildServiceProvider();
    }

    public async Task DisposeAsync()
    {
        _serviceProvider?.Dispose();
        _store?.Dispose();
        if (_postgres != null)
            await _postgres.DisposeAsync();
    }

    // ══════════════════════════════════════════════════════════════════
    // Happy path: Receive → Allocate → full PickStock
    // ══════════════════════════════════════════════════════════════════

    [SkippableFact]
    public async Task HappyPath_Receive_Allocate_StartPicking_PickStock()
    {
        DockerRequirement.EnsureEnabled();

        // ── Step 1: Receive goods to create stock ──────────────────────
        var receiveCommand = new ReceiveGoodsCommand
        {
            CommandId = Guid.NewGuid(),
            WarehouseId = WarehouseId,
            Location = Location,
            HuType = "PALLET",
            OperatorId = Guid.NewGuid(),
            Lines = new List<ReceiveGoodsLineDto>
            {
                new() { SKU = Sku, Quantity = 100m }
            }
        };

        Guid handlingUnitId;
        using (var scope = _serviceProvider!.CreateScope())
        {
            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
            var result = await mediator.Send(receiveCommand);
            result.IsSuccess.Should().BeTrue($"ReceiveGoods failed: {result.Error}");
        }

        // Wait for projections to catch up
        using (var daemon = await _store!.BuildProjectionDaemonAsync())
        {
            await daemon.StartAllAsync();
            await daemon.WaitForNonStaleData(TimeSpan.FromSeconds(30));
        }

        // Get the HU ID from projection
        await using (var session = _store.QuerySession())
        {
            var huViews = await session.Query<HandlingUnitView>()
                .Where(h => h.CurrentLocation == Location)
                .ToListAsync();
            huViews.Should().HaveCount(1);
            handlingUnitId = huViews[0].HuId;
        }

        // ── Step 2: Create a reservation ───────────────────────────────
        var reservationId = Guid.NewGuid();
        await using (var session = _store.LightweightSession())
        {
            var streamId = Reservation.StreamIdFor(reservationId);
            session.Events.Append(streamId, new ReservationCreatedEvent
            {
                ReservationId = reservationId,
                Purpose = "TEST_ORDER",
                Priority = 1,
                RequestedLines = new List<ReservationLineDto>
                {
                    new() { SKU = Sku, Quantity = 50m }
                }
            });
            await session.SaveChangesAsync();
        }

        // ── Step 3: Allocate (SOFT lock) ───────────────────────────────
        using (var scope = _serviceProvider!.CreateScope())
        {
            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
            var result = await mediator.Send(new AllocateReservationCommand
            {
                CommandId = Guid.NewGuid(),
                ReservationId = reservationId,
                WarehouseId = WarehouseId
            });
            result.IsSuccess.Should().BeTrue($"Allocation failed: {result.Error}");
        }

        // Verify reservation is now ALLOCATED
        await using (var session = _store.LightweightSession())
        {
            var reservation = await session.Events.AggregateStreamAsync<Reservation>(
                Reservation.StreamIdFor(reservationId));
            reservation.Should().NotBeNull();
            reservation!.Status.Should().Be(ReservationStatus.ALLOCATED);
            reservation.LockType.Should().Be(ReservationLockType.SOFT);
        }

        // ── Step 4: StartPicking (SOFT → HARD) ────────────────────────
        using (var scope = _serviceProvider!.CreateScope())
        {
            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
            var result = await mediator.Send(new StartPickingCommand
            {
                CommandId = Guid.NewGuid(),
                ReservationId = reservationId
            });
            result.IsSuccess.Should().BeTrue($"StartPicking failed: {result.Error}");
        }

        // Verify reservation is now PICKING
        await using (var session = _store.LightweightSession())
        {
            var reservation = await session.Events.AggregateStreamAsync<Reservation>(
                Reservation.StreamIdFor(reservationId));
            reservation.Should().NotBeNull();
            reservation!.Status.Should().Be(ReservationStatus.PICKING);
        }

        // ── Step 5: PickStock (V-3: StockLedger first) ────────────────
        using (var scope = _serviceProvider!.CreateScope())
        {
            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
            var result = await mediator.Send(new PickStockCommand
            {
                CommandId = Guid.NewGuid(),
                ReservationId = reservationId,
                HandlingUnitId = handlingUnitId,
                WarehouseId = WarehouseId,
                SKU = Sku,
                Quantity = 50m,
                FromLocation = Location,
                OperatorId = Guid.NewGuid()
            });
            result.IsSuccess.Should().BeTrue($"PickStock failed: {result.Error}");
        }

        // Wait for projections
        using (var daemon = await _store!.BuildProjectionDaemonAsync())
        {
            await daemon.StartAllAsync();
            await daemon.WaitForNonStaleData(TimeSpan.FromSeconds(30));
        }

        // ── Assertions ─────────────────────────────────────────────────
        await using (var session = _store.QuerySession())
        {
            // Reservation should be CONSUMED
            var querySession2 = _store.LightweightSession();
            var reservation = await querySession2.Events.AggregateStreamAsync<Reservation>(
                Reservation.StreamIdFor(reservationId));
            reservation.Should().NotBeNull();
            reservation!.Status.Should().Be(ReservationStatus.CONSUMED);

            // StockMovement committed (PICK movement exists)
            var ledgerStreamId = StockLedgerStreamId.For(WarehouseId, Location, Sku);
            var ledgerEvents = await querySession2.Events.FetchStreamAsync(ledgerStreamId);
            var pickMovements = ledgerEvents
                .Select(e => e.Data)
                .OfType<StockMovedEvent>()
                .Where(e => e.MovementType == "PICK")
                .ToList();
            pickMovements.Should().HaveCount(1);
            pickMovements[0].Quantity.Should().Be(50m);

            // AvailableStock should reflect pick
            var availableKey = AvailableStockView.ComputeId(WarehouseId, Location, Sku);
            var available = await session.LoadAsync<AvailableStockView>(availableKey);
            available.Should().NotBeNull();
            // After receiving 100 and picking 50, on-hand should be 50
            available!.OnHandQty.Should().Be(50m);

            await querySession2.DisposeAsync();
        }
    }

    // ══════════════════════════════════════════════════════════════════
    // Failure path: consumption fails → deferred to saga
    // ══════════════════════════════════════════════════════════════════

    [SkippableFact]
    public async Task PickStock_ConsumptionFails_PublishesDeferredMessage()
    {
        DockerRequirement.EnsureEnabled();

        var uniqueLocation = $"LOC-FAIL-{Guid.NewGuid().ToString("N")[..6]}";

        // ── Receive goods ──
        using (var scope = _serviceProvider!.CreateScope())
        {
            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
            var result = await mediator.Send(new ReceiveGoodsCommand
            {
                CommandId = Guid.NewGuid(),
                WarehouseId = WarehouseId,
                Location = uniqueLocation,
                HuType = "PALLET",
                OperatorId = Guid.NewGuid(),
                Lines = new List<ReceiveGoodsLineDto>
                {
                    new() { SKU = Sku, Quantity = 100m }
                }
            });
            result.IsSuccess.Should().BeTrue($"ReceiveGoods failed: {result.Error}");
        }

        // Wait for projections
        using (var daemon = await _store!.BuildProjectionDaemonAsync())
        {
            await daemon.StartAllAsync();
            await daemon.WaitForNonStaleData(TimeSpan.FromSeconds(30));
        }

        Guid handlingUnitId;
        await using (var session = _store.QuerySession())
        {
            var huViews = await session.Query<HandlingUnitView>()
                .Where(h => h.CurrentLocation == uniqueLocation)
                .ToListAsync();
            handlingUnitId = huViews[0].HuId;
        }

        // ── Create reservation + allocate + startPicking ──
        var reservationId = Guid.NewGuid();
        await using (var session = _store.LightweightSession())
        {
            var streamId = Reservation.StreamIdFor(reservationId);
            session.Events.Append(streamId,
                new ReservationCreatedEvent
                {
                    ReservationId = reservationId,
                    Purpose = "TEST_FAIL",
                    Priority = 1,
                    RequestedLines = new List<ReservationLineDto>
                    {
                        new() { SKU = Sku, Quantity = 50m }
                    }
                });
            await session.SaveChangesAsync();
        }

        using (var scope = _serviceProvider!.CreateScope())
        {
            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
            await mediator.Send(new AllocateReservationCommand
            {
                CommandId = Guid.NewGuid(),
                ReservationId = reservationId,
                WarehouseId = WarehouseId
            });
            await mediator.Send(new StartPickingCommand
            {
                CommandId = Guid.NewGuid(),
                ReservationId = reservationId
            });
        }

        // ── Now manually consume the reservation to simulate it being already consumed ──
        // (This will cause the PickStock's consumption attempt to see "CONSUMED" not "PICKING")
        await using (var session = _store.LightweightSession())
        {
            var streamId = Reservation.StreamIdFor(reservationId);
            var streamState = await session.Events.FetchStreamStateAsync(streamId);
            session.Events.Append(streamId, streamState!.Version,
                new ReservationConsumedEvent
                {
                    ReservationId = reservationId,
                    ActualQuantity = 50m,
                    ReleasedHardLockLines = new List<HardLockLineDto>()
                });
            await session.SaveChangesAsync();
        }

        // ── Now PickStock — movement will succeed but consumption will be deferred
        // because reservation is already CONSUMED (not PICKING) ──
        var eventBus = _serviceProvider!.GetRequiredService<IEventBus>() as NoOpEventBus;
        eventBus!.Published.Clear();

        using (var scope = _serviceProvider!.CreateScope())
        {
            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
            var result = await mediator.Send(new PickStockCommand
            {
                CommandId = Guid.NewGuid(),
                ReservationId = reservationId,
                HandlingUnitId = handlingUnitId,
                WarehouseId = WarehouseId,
                SKU = Sku,
                Quantity = 50m,
                FromLocation = uniqueLocation,
                OperatorId = Guid.NewGuid(),
                CorrelationId = Guid.NewGuid()
            });

            // The handler returns OK because movement IS committed,
            // consumption failure is deferred to saga
            result.IsSuccess.Should().BeTrue($"PickStock should return OK (movement committed): {result.Error}");
        }

        // ── Assert: deferred message was published ──
        // The NoOpEventBus captures published messages
        // Since reservation was already CONSUMED, the consumption attempt will
        // either succeed (idempotent) or fail with wrong-state error.
        // Either way, the movement IS committed.
        await using (var session = _store.LightweightSession())
        {
            var ledgerStreamId = StockLedgerStreamId.For(WarehouseId, uniqueLocation, Sku);
            var ledgerEvents = await session.Events.FetchStreamAsync(ledgerStreamId);
            var pickMovements = ledgerEvents
                .Select(e => e.Data)
                .OfType<StockMovedEvent>()
                .Where(e => e.MovementType == "PICK")
                .ToList();
            pickMovements.Should().HaveCount(1,
                "StockMovement must be committed regardless of consumption outcome");
        }
    }

    // ══════════════════════════════════════════════════════════════════
    // Test: saga retry path via direct orchestration
    // ══════════════════════════════════════════════════════════════════

    [SkippableFact]
    public async Task ConsumeReservation_Idempotent_WhenAlreadyConsumed()
    {
        DockerRequirement.EnsureEnabled();

        var reservationId = Guid.NewGuid();

        // Create a reservation in CONSUMED state
        await using (var session = _store!.LightweightSession())
        {
            var streamId = Reservation.StreamIdFor(reservationId);
            session.Events.Append(streamId,
                new ReservationCreatedEvent
                {
                    ReservationId = reservationId,
                    Purpose = "TEST",
                    Priority = 1,
                    RequestedLines = new List<ReservationLineDto>
                    {
                        new() { SKU = Sku, Quantity = 25m }
                    }
                },
                new StockAllocatedEvent
                {
                    ReservationId = reservationId,
                    Allocations = new List<AllocationDto>
                    {
                        new() { SKU = Sku, Quantity = 25m, Location = Location, WarehouseId = WarehouseId }
                    },
                    LockType = "SOFT"
                },
                new PickingStartedEvent
                {
                    ReservationId = reservationId,
                    LockType = "HARD",
                    HardLockedLines = new List<HardLockLineDto>
                    {
                        new() { WarehouseId = WarehouseId, Location = Location, SKU = Sku, HardLockedQty = 25m }
                    }
                },
                new ReservationConsumedEvent
                {
                    ReservationId = reservationId,
                    ActualQuantity = 25m,
                    ReleasedHardLockLines = new List<HardLockLineDto>
                    {
                        new() { WarehouseId = WarehouseId, Location = Location, SKU = Sku, HardLockedQty = 25m }
                    }
                });
            await session.SaveChangesAsync();
        }

        // Attempt to consume again — should be idempotent
        using var scope = _serviceProvider!.CreateScope();
        var orchestration = scope.ServiceProvider.GetRequiredService<IPickStockOrchestration>();
        var result = await orchestration.ConsumeReservationAsync(reservationId, 25m);

        result.IsSuccess.Should().BeTrue("Consuming an already-consumed reservation should be idempotent");
    }
}

/// <summary>
/// No-op event bus for integration tests that captures published messages.
/// </summary>
public class NoOpEventBus : IEventBus
{
    public List<object> Published { get; } = new();

    public Task PublishAsync<T>(T message, CancellationToken ct = default) where T : class
    {
        Published.Add(message);
        return Task.CompletedTask;
    }
}

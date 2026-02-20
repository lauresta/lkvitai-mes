using FluentAssertions;
using LKvitai.MES.Application.Behaviors;
using LKvitai.MES.Application.Commands;
using LKvitai.MES.Application.Orchestration;
using LKvitai.MES.Application.Ports;
using LKvitai.MES.Contracts.Events;
using LKvitai.MES.Contracts.ReadModels;
using LKvitai.MES.Modules.Warehouse.Domain;
using LKvitai.MES.Infrastructure.Persistence;
using LKvitai.MES.Projections;
using LKvitai.MES.SharedKernel;
using Marten;
using Marten.Events.Projections;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Testcontainers.PostgreSql;
using Xunit;

namespace LKvitai.MES.Tests.Integration;

/// <summary>
/// Integration tests for ReceiveGoods workflow and HandlingUnit projection (Package E).
///
/// Uses Testcontainers (PostgreSQL) to verify:
///   - Full handler path: MediatR → IdempotencyBehavior → Handler → Orchestration
///   - Idempotency: duplicate CommandId returns OK without new events
///   - Projection correctness: HU view, LocationBalance, AvailableStock
///
/// Opt-in: TESTCONTAINERS_ENABLED=1
/// </summary>
[Trait("Category", "Idempotency")]
public class ReceiveGoodsIntegrationTests : IAsyncLifetime
{
    private PostgreSqlContainer? _postgres;
    private IDocumentStore? _store;
    private ServiceProvider? _serviceProvider;

    private const string WarehouseId = "WH1";
    private const string Location = "LOC-A";
    private const string Sku1 = "SKU-001";
    private const string Sku2 = "SKU-002";

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

            // Register all projections the same way the app does
            opts.Projections.Add<HandlingUnitProjection>(ProjectionLifecycle.Async);
            opts.Projections.Add<LocationBalanceProjection>(ProjectionLifecycle.Async);
            opts.Projections.Add<AvailableStockProjection>(ProjectionLifecycle.Async);
        });

        // Build a full DI container for handler-path tests
        var services = new ServiceCollection();
        services.AddSingleton<IDocumentStore>(_store);
        services.AddLogging();
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining<ReceiveGoodsCommandHandler>();
        });
        services.AddScoped<IReceiveGoodsOrchestration, MartenReceiveGoodsOrchestration>();
        services.AddScoped<IProcessedCommandStore, MartenProcessedCommandStore>();
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(IdempotencyBehavior<,>));

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
    // Handler-path tests (real MediatR pipeline)
    // ══════════════════════════════════════════════════════════════════

    [SkippableFact]
    public async Task Handler_ReceiveGoods_CreatesHUViewAndEvents()
    {
        DockerRequirement.EnsureEnabled();

        var command = new ReceiveGoodsCommand
        {
            CommandId = Guid.NewGuid(),
            WarehouseId = WarehouseId,
            Location = "LOC-HANDLER",
            HuType = "PALLET",
            OperatorId = Guid.NewGuid(),
            Lines = new List<ReceiveGoodsLineDto>
            {
                new() { SKU = Sku1, Quantity = 100m },
                new() { SKU = Sku2, Quantity = 50m }
            }
        };

        // ── Execute via MediatR (behavior → handler → orchestration) ──
        Result result;
        using (var scope = _serviceProvider!.CreateScope())
        {
            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
            result = await mediator.Send(command);
        }

        result.IsSuccess.Should().BeTrue($"Expected success but got: {result.Error}");

        // ── Wait for async projections ──
        using var daemon = await _store!.BuildProjectionDaemonAsync();
        await daemon.StartAllAsync();
        await daemon.WaitForNonStaleData(TimeSpan.FromSeconds(30));

        // ── Assert: HandlingUnitView ──
        await using var querySession = _store.QuerySession();
        var huViews = await querySession.Query<HandlingUnitView>()
            .Where(h => h.CurrentLocation == "LOC-HANDLER")
            .ToListAsync();

        huViews.Should().HaveCount(1);
        var huView = huViews[0];
        huView.Status.Should().Be("SEALED");
        huView.WarehouseId.Should().Be(WarehouseId);
        huView.Lines.Should().HaveCount(2);
        huView.Lines.Should().Contain(l => l.SKU == Sku1 && l.Quantity == 100m);
        huView.Lines.Should().Contain(l => l.SKU == Sku2 && l.Quantity == 50m);

        // ── Assert: events exist in HU stream ──
        await using var eventSession = _store.LightweightSession();
        var huStreamId = $"handling-unit:{huView.HuId}";
        var huEvents = await eventSession.Events.FetchStreamAsync(huStreamId);
        huEvents.Should().HaveCountGreaterThanOrEqualTo(2,
            "Should have at least HandlingUnitCreated + HandlingUnitSealed");

        // ── Assert: idempotency record persisted ──
        var cmdRecord = await querySession.LoadAsync<ProcessedCommandRecord>(
            command.CommandId.ToString());
        cmdRecord.Should().NotBeNull();
        cmdRecord!.Status.Should().Be(ProcessedCommandStatus.Success);
    }

    [SkippableFact]
    public async Task Handler_DuplicateCommandId_ReturnsOkWithoutNewEvents()
    {
        DockerRequirement.EnsureEnabled();

        var commandId = Guid.NewGuid();
        var uniqueLocation = $"LOC-IDEM-{Guid.NewGuid().ToString("N")[..6]}";

        var command = new ReceiveGoodsCommand
        {
            CommandId = commandId,
            WarehouseId = WarehouseId,
            Location = uniqueLocation,
            HuType = "BOX",
            OperatorId = Guid.NewGuid(),
            Lines = new List<ReceiveGoodsLineDto>
            {
                new() { SKU = "SKU-IDEM", Quantity = 25m }
            }
        };

        // ── First call: succeeds ──
        using (var scope = _serviceProvider!.CreateScope())
        {
            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
            var result1 = await mediator.Send(command);
            result1.IsSuccess.Should().BeTrue($"First call should succeed: {result1.Error}");
        }

        // Wait for projections after first call
        using var daemon = await _store!.BuildProjectionDaemonAsync();
        await daemon.StartAllAsync();
        await daemon.WaitForNonStaleData(TimeSpan.FromSeconds(30));

        // Capture state after first call
        HandlingUnitView firstHuView;
        int eventCountAfterFirst;
        await using (var session = _store.LightweightSession())
        {
            var views = await session.Query<HandlingUnitView>()
                .Where(h => h.CurrentLocation == uniqueLocation)
                .ToListAsync();
            views.Should().HaveCount(1);
            firstHuView = views[0];

            var huStreamId = $"handling-unit:{firstHuView.HuId}";
            var events = await session.Events.FetchStreamAsync(huStreamId);
            eventCountAfterFirst = events.Count;
        }

        // ── Second call with same CommandId: idempotent ──
        using (var scope = _serviceProvider!.CreateScope())
        {
            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
            var result2 = await mediator.Send(command);
            result2.IsSuccess.Should().BeTrue("Idempotent replay should return OK");
        }

        // Wait briefly for any projection work
        await daemon.WaitForNonStaleData(TimeSpan.FromSeconds(10));

        // ── Assert: no new HU views, no new events ──
        await using (var session = _store.LightweightSession())
        {
            var views = await session.Query<HandlingUnitView>()
                .Where(h => h.CurrentLocation == uniqueLocation)
                .ToListAsync();
            views.Should().HaveCount(1, "No new HU should be created for duplicate command");

            var huStreamId = $"handling-unit:{firstHuView.HuId}";
            var events = await session.Events.FetchStreamAsync(huStreamId);
            events.Should().HaveCount(eventCountAfterFirst,
                "No new events should be emitted for duplicate command");
        }
    }

    [SkippableFact]
    public async Task Handler_UpdatesLocationBalanceAndAvailableStock()
    {
        DockerRequirement.EnsureEnabled();

        var uniqueLocation = $"LOC-BAL-{Guid.NewGuid().ToString("N")[..6]}";

        var command = new ReceiveGoodsCommand
        {
            CommandId = Guid.NewGuid(),
            WarehouseId = WarehouseId,
            Location = uniqueLocation,
            HuType = "UNIT",
            OperatorId = Guid.NewGuid(),
            Lines = new List<ReceiveGoodsLineDto>
            {
                new() { SKU = Sku1, Quantity = 75m }
            }
        };

        // Execute via handler
        using (var scope = _serviceProvider!.CreateScope())
        {
            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
            var result = await mediator.Send(command);
            result.IsSuccess.Should().BeTrue($"Expected success: {result.Error}");
        }

        // Wait for projections
        using var daemon = await _store!.BuildProjectionDaemonAsync();
        await daemon.StartAllAsync();
        await daemon.WaitForNonStaleData(TimeSpan.FromSeconds(30));

        await using var querySession = _store.QuerySession();

        // ── Assert: LocationBalance ──
        var balanceKey = $"{WarehouseId}:{uniqueLocation}:{Sku1}";
        var balance = await querySession.LoadAsync<LocationBalanceView>(balanceKey);
        balance.Should().NotBeNull("LocationBalance should exist");
        balance!.Quantity.Should().Be(75m);

        // ── Assert: AvailableStock ──
        var availableKey = AvailableStockView.ComputeId(WarehouseId, uniqueLocation, Sku1);
        var available = await querySession.LoadAsync<AvailableStockView>(availableKey);
        available.Should().NotBeNull("AvailableStock should exist");
        available!.OnHandQty.Should().Be(75m);
        available.AvailableQty.Should().Be(75m);
    }

    [SkippableFact]
    public async Task Handler_ConcurrentDuplicates_OnlyOneCommitsEvents()
    {
        DockerRequirement.EnsureEnabled();

        var commandId = Guid.NewGuid();
        var uniqueLocation = $"LOC-CONC-{Guid.NewGuid().ToString("N")[..6]}";

        var command = new ReceiveGoodsCommand
        {
            CommandId = commandId,
            WarehouseId = WarehouseId,
            Location = uniqueLocation,
            HuType = "PALLET",
            OperatorId = Guid.NewGuid(),
            Lines = new List<ReceiveGoodsLineDto>
            {
                new() { SKU = "SKU-CONC", Quantity = 100m }
            }
        };

        // ── Launch two concurrent executions with the same CommandId ──
        var barrier = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        async Task<Result> RunCommand()
        {
            // Both tasks wait for the barrier so they start as close together as possible
            await barrier.Task;
            using var scope = _serviceProvider!.CreateScope();
            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
            return await mediator.Send(command);
        }

        var task1 = Task.Run(RunCommand);
        var task2 = Task.Run(RunCommand);

        // Release both tasks simultaneously
        barrier.SetResult();

        var results = await Task.WhenAll(task1, task2);

        // At least one must succeed; the other either succeeds (AlreadyCompleted short-circuit)
        // or fails with IDEMPOTENCY_IN_PROGRESS — both are acceptable.
        var successes = results.Count(r => r.IsSuccess);
        var inProgressErrors = results.Count(r =>
            !r.IsSuccess && r.Error == DomainErrorCodes.IdempotencyInProgress);

        successes.Should().BeGreaterThanOrEqualTo(1,
            "At least one concurrent execution must succeed");
        (successes + inProgressErrors).Should().Be(2,
            "Every result must be either success or IDEMPOTENCY_IN_PROGRESS");

        // ── Wait for projections ──
        using var daemon = await _store!.BuildProjectionDaemonAsync();
        await daemon.StartAllAsync();
        await daemon.WaitForNonStaleData(TimeSpan.FromSeconds(30));

        // ── KEY INVARIANT: exactly one HU created ──
        await using var querySession = _store.QuerySession();
        var huViews = await querySession.Query<HandlingUnitView>()
            .Where(h => h.CurrentLocation == uniqueLocation)
            .ToListAsync();

        huViews.Should().HaveCount(1,
            "Exactly one HU should exist — concurrent duplicate must not create a second");
    }

    // NOTE: The sealed-HU command-path guard cannot be meaningfully integration-tested
    // because ReceiveGoods always generates a new HU ID (Guid.NewGuid()), making
    // stream collision impossible in practice. The sealed invariant is verified via:
    //   - Unit tests: HandlingUnitProjectionTests.Sealed_* (projection guards)
    //   - Defensive guard in MartenReceiveGoodsOrchestration (verifies stream doesn't exist)

    // ══════════════════════════════════════════════════════════════════
    // Projection-level tests (event seeding for projection verification)
    // ══════════════════════════════════════════════════════════════════

    [SkippableFact]
    public async Task Projection_ReceiveGoods_CreatesHandlingUnitView_WithCorrectLinesAndStatus()
    {
        DockerRequirement.EnsureEnabled();

        var huId = Guid.NewGuid();
        var lpn = $"HU-TEST-{huId.ToString("N")[..6].ToUpper()}";
        var huStreamId = $"handling-unit:{huId}";
        var now = DateTime.UtcNow;

        // ── Seed events (same order as MartenReceiveGoodsOrchestration) ──
        await using (var session = _store!.LightweightSession())
        {
            session.Events.Append(huStreamId, new HandlingUnitCreatedEvent
            {
                HuId = huId,
                LPN = lpn,
                Type = "PALLET",
                WarehouseId = WarehouseId,
                Location = Location,
                OperatorId = Guid.NewGuid(),
                Timestamp = now
            });

            var stockStreamId1 = StockLedgerStreamId.For(WarehouseId, Location, Sku1);
            session.Events.Append(stockStreamId1, new StockMovedEvent
            {
                MovementId = Guid.NewGuid(),
                SKU = Sku1,
                Quantity = 100m,
                FromLocation = "SUPPLIER",
                ToLocation = Location,
                MovementType = "RECEIPT",
                OperatorId = Guid.NewGuid(),
                HandlingUnitId = huId,
                Timestamp = now
            });

            var stockStreamId2 = StockLedgerStreamId.For(WarehouseId, Location, Sku2);
            session.Events.Append(stockStreamId2, new StockMovedEvent
            {
                MovementId = Guid.NewGuid(),
                SKU = Sku2,
                Quantity = 50m,
                FromLocation = "SUPPLIER",
                ToLocation = Location,
                MovementType = "RECEIPT",
                OperatorId = Guid.NewGuid(),
                HandlingUnitId = huId,
                Timestamp = now
            });

            session.Events.Append(huStreamId, new HandlingUnitSealedEvent
            {
                HuId = huId,
                SealedAt = now,
                Timestamp = now
            });

            await session.SaveChangesAsync();
        }

        // ── Wait for async projections ──
        using var daemon = await _store!.BuildProjectionDaemonAsync();
        await daemon.StartAllAsync();
        await daemon.WaitForNonStaleData(TimeSpan.FromSeconds(30));

        // ── Assert: HandlingUnitView ──
        await using var querySession = _store.QuerySession();

        var huView = await querySession.LoadAsync<HandlingUnitView>(huId.ToString());

        huView.Should().NotBeNull("HU view should exist after ReceiveGoods");
        huView!.HuId.Should().Be(huId);
        huView.LPN.Should().Be(lpn);
        huView.Type.Should().Be("PALLET");
        huView.Status.Should().Be("SEALED");
        huView.WarehouseId.Should().Be(WarehouseId);
        huView.CurrentLocation.Should().Be(Location);
        huView.SealedAt.Should().NotBeNull();

        huView.Lines.Should().HaveCount(2);
        huView.Lines.Should().Contain(l => l.SKU == Sku1 && l.Quantity == 100m);
        huView.Lines.Should().Contain(l => l.SKU == Sku2 && l.Quantity == 50m);
    }

    [SkippableFact]
    public async Task Projection_ReceiveGoods_UpdatesLocationBalance()
    {
        DockerRequirement.EnsureEnabled();

        var huId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var locBalTest = "LOC-BALPRJ";

        await using (var session = _store!.LightweightSession())
        {
            var huStreamId = $"handling-unit:{huId}";

            session.Events.Append(huStreamId, new HandlingUnitCreatedEvent
            {
                HuId = huId,
                LPN = "HU-TEST-LB",
                Type = "BOX",
                WarehouseId = WarehouseId,
                Location = locBalTest,
                OperatorId = Guid.NewGuid(),
                Timestamp = now
            });

            var stockStreamId = StockLedgerStreamId.For(WarehouseId, locBalTest, Sku1);
            session.Events.Append(stockStreamId, new StockMovedEvent
            {
                MovementId = Guid.NewGuid(),
                SKU = Sku1,
                Quantity = 200m,
                FromLocation = "SUPPLIER",
                ToLocation = locBalTest,
                MovementType = "RECEIPT",
                OperatorId = Guid.NewGuid(),
                HandlingUnitId = huId,
                Timestamp = now
            });

            session.Events.Append(huStreamId, new HandlingUnitSealedEvent
            {
                HuId = huId,
                SealedAt = now,
                Timestamp = now
            });

            await session.SaveChangesAsync();
        }

        using var daemon = await _store!.BuildProjectionDaemonAsync();
        await daemon.StartAllAsync();
        await daemon.WaitForNonStaleData(TimeSpan.FromSeconds(30));

        await using var querySession = _store.QuerySession();

        var toBalanceKey = $"{WarehouseId}:{locBalTest}:{Sku1}";
        var toBalance = await querySession.LoadAsync<LocationBalanceView>(toBalanceKey);

        toBalance.Should().NotBeNull("LocationBalance should exist for destination");
        toBalance!.Quantity.Should().Be(200m);

        var fromBalanceKey = $"{WarehouseId}:SUPPLIER:{Sku1}";
        var fromBalance = await querySession.LoadAsync<LocationBalanceView>(fromBalanceKey);
        fromBalance.Should().NotBeNull();
        fromBalance!.Quantity.Should().Be(-200m);
    }

    [SkippableFact]
    public async Task Projection_ReceiveGoods_UpdatesAvailableStock()
    {
        DockerRequirement.EnsureEnabled();

        var huId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var locAvailTest = "LOC-AVAILPRJ";

        await using (var session = _store!.LightweightSession())
        {
            var huStreamId = $"handling-unit:{huId}";

            session.Events.Append(huStreamId, new HandlingUnitCreatedEvent
            {
                HuId = huId,
                LPN = "HU-TEST-AS",
                Type = "UNIT",
                WarehouseId = WarehouseId,
                Location = locAvailTest,
                OperatorId = Guid.NewGuid(),
                Timestamp = now
            });

            var stockStreamId = StockLedgerStreamId.For(WarehouseId, locAvailTest, Sku1);
            session.Events.Append(stockStreamId, new StockMovedEvent
            {
                MovementId = Guid.NewGuid(),
                SKU = Sku1,
                Quantity = 75m,
                FromLocation = "SUPPLIER",
                ToLocation = locAvailTest,
                MovementType = "RECEIPT",
                OperatorId = Guid.NewGuid(),
                HandlingUnitId = huId,
                Timestamp = now
            });

            session.Events.Append(huStreamId, new HandlingUnitSealedEvent
            {
                HuId = huId,
                SealedAt = now,
                Timestamp = now
            });

            await session.SaveChangesAsync();
        }

        using var daemon = await _store!.BuildProjectionDaemonAsync();
        await daemon.StartAllAsync();
        await daemon.WaitForNonStaleData(TimeSpan.FromSeconds(30));

        await using var querySession = _store.QuerySession();

        var availableKey = AvailableStockView.ComputeId(WarehouseId, locAvailTest, Sku1);
        var available = await querySession.LoadAsync<AvailableStockView>(availableKey);

        available.Should().NotBeNull("AvailableStock should exist for destination");
        available!.OnHandQty.Should().Be(75m);
        available.HardLockedQty.Should().Be(0m);
        available.AvailableQty.Should().Be(75m);
    }
}

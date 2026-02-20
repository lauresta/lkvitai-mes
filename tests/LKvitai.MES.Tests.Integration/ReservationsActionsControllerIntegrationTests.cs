using FluentAssertions;
using LKvitai.MES.Api.Controllers;
using LKvitai.MES.Modules.Warehouse.Application.Commands;
using LKvitai.MES.Modules.Warehouse.Application.Orchestration;
using LKvitai.MES.Modules.Warehouse.Application.Ports;
using LKvitai.MES.Contracts.Events;
using LKvitai.MES.Modules.Warehouse.Domain;
using LKvitai.MES.Modules.Warehouse.Domain.Aggregates;
using LKvitai.MES.Modules.Warehouse.Infrastructure.Persistence;
using LKvitai.MES.Modules.Warehouse.Projections;
using LKvitai.MES.SharedKernel;
using Marten;
using Marten.Events.Projections;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;
using Xunit;

namespace LKvitai.MES.Tests.Integration;

public class ReservationsActionsControllerIntegrationTests : IAsyncLifetime
{
    private PostgreSqlContainer? _postgres;
    private IDocumentStore? _store;
    private ServiceProvider? _provider;

    public async Task InitializeAsync()
    {
        if (!DockerRequirement.IsEnabled)
        {
            return;
        }

        _postgres = new PostgreSqlBuilder()
            .WithImage("postgres:16-alpine")
            .Build();

        await _postgres.StartAsync();

        _store = DocumentStore.For(opts =>
        {
            opts.Connection(_postgres.GetConnectionString());
            opts.Events.StreamIdentity = Marten.Events.StreamIdentity.AsString;
            opts.Events.DatabaseSchemaName = "warehouse_events";

            opts.Projections.Add<ActiveHardLocksProjection>(ProjectionLifecycle.Inline);
            opts.Projections.Add<HandlingUnitProjection>(ProjectionLifecycle.Async);
        });

        await _store.Storage.ApplyAllConfiguredChangesToDatabaseAsync();

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:WarehouseDb"] = _postgres.GetConnectionString()
            })
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IDocumentStore>(_store);
        services.AddSingleton<IConfiguration>(config);
        services.AddSingleton<IBalanceGuardLockFactory, PostgresBalanceGuardLockFactory>();
        services.AddLogging();
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining<StartPickingCommandHandler>();
        });

        services.AddScoped<IReservationRepository, MartenReservationRepository>();
        services.AddScoped<IStartPickingOrchestration, MartenStartPickingOrchestration>();
        services.AddScoped<IPickStockOrchestration, MartenPickStockOrchestration>();
        services.AddScoped<IEventBus, NoOpEventBus>();

        _provider = services.BuildServiceProvider();
    }

    public async Task DisposeAsync()
    {
        _provider?.Dispose();
        _store?.Dispose();
        if (_postgres is not null)
        {
            await _postgres.DisposeAsync();
        }
    }

    [SkippableFact]
    public async Task StartPicking_HappyPath_ReturnsOkResponse()
    {
        DockerRequirement.EnsureEnabled();

        var reservationId = Guid.NewGuid();
        var huId = Guid.NewGuid();
        const string warehouseId = "WH1";
        const string location = "LOC-A";
        const string sku = "SKU-START-OK";

        await SeedStockAsync(warehouseId, location, sku, 100m);
        await SeedReservationAsync(reservationId, huId, warehouseId, location, sku, 25m, status: ReservationStatus.ALLOCATED);

        using var scope = _provider!.CreateScope();
        var controller = CreateController(scope.ServiceProvider);

        var result = await controller.StartPickingAsync(
            reservationId,
            new ReservationsController.StartPickingRequestDto(reservationId),
            CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();

        await using var query = _store!.QuerySession();
        var reservation = await query.Events.AggregateStreamAsync<Reservation>(Reservation.StreamIdFor(reservationId));
        reservation.Should().NotBeNull();
        reservation!.Status.Should().Be(ReservationStatus.PICKING);
    }

    [SkippableFact]
    public async Task StartPicking_NotAllocated_ReturnsProblemDetails400()
    {
        DockerRequirement.EnsureEnabled();

        var reservationId = Guid.NewGuid();
        await SeedReservationCreatedOnlyAsync(reservationId);

        using var scope = _provider!.CreateScope();
        var controller = CreateController(scope.ServiceProvider);

        var result = await controller.StartPickingAsync(
            reservationId,
            new ReservationsController.StartPickingRequestDto(reservationId),
            CancellationToken.None);

        var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        GetErrorCode(objectResult.Value).Should().Be(DomainErrorCodes.ReservationNotAllocated);
    }

    [SkippableFact]
    public async Task Pick_HappyPath_ReturnsOkResponse()
    {
        DockerRequirement.EnsureEnabled();

        var reservationId = Guid.NewGuid();
        var huId = Guid.NewGuid();
        const string warehouseId = "WH1";
        const string location = "LOC-PICK";
        const string sku = "SKU-PICK-OK";

        await SeedStockAsync(warehouseId, location, sku, 100m);
        await SeedReservationAsync(reservationId, huId, warehouseId, location, sku, 40m, status: ReservationStatus.PICKING);

        using var scope = _provider!.CreateScope();
        var controller = CreateController(scope.ServiceProvider);

        var result = await controller.PickAsync(
            reservationId,
            new ReservationsController.PickRequestDto(reservationId, huId, sku, 20m),
            CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
    }

    [SkippableFact]
    public async Task Pick_InsufficientBalance_ReturnsProblemDetails422()
    {
        DockerRequirement.EnsureEnabled();

        var reservationId = Guid.NewGuid();
        var huId = Guid.NewGuid();
        const string warehouseId = "WH1";
        const string location = "LOC-PICK-LOW";
        const string sku = "SKU-PICK-LOW";

        // Intentionally keep ledger balance below requested pick quantity.
        await SeedStockAsync(warehouseId, location, sku, 10m);
        await SeedReservationAsync(reservationId, huId, warehouseId, location, sku, 100m, status: ReservationStatus.PICKING);

        using var scope = _provider!.CreateScope();
        var controller = CreateController(scope.ServiceProvider);

        var result = await controller.PickAsync(
            reservationId,
            new ReservationsController.PickRequestDto(reservationId, huId, sku, 50m),
            CancellationToken.None);

        var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status422UnprocessableEntity);
        GetErrorCode(objectResult.Value).Should().Be(DomainErrorCodes.InsufficientBalance);
    }

    private ReservationsController CreateController(IServiceProvider services)
    {
        var mediator = services.GetRequiredService<IMediator>();
        var reservationRepository = services.GetRequiredService<IReservationRepository>();

        return new ReservationsController(mediator, reservationRepository)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };
    }

    private async Task SeedStockAsync(string warehouseId, string location, string sku, decimal quantity)
    {
        await using var session = _store!.LightweightSession();
        session.Events.Append(StockLedgerStreamId.For(warehouseId, location, sku), new StockMovedEvent
        {
            MovementId = Guid.NewGuid(),
            SKU = sku,
            Quantity = quantity,
            FromLocation = "SUPPLIER",
            ToLocation = location,
            MovementType = "RECEIPT",
            OperatorId = Guid.NewGuid(),
            Timestamp = DateTime.UtcNow
        });
        await session.SaveChangesAsync();
    }

    private async Task SeedReservationCreatedOnlyAsync(Guid reservationId)
    {
        await using var session = _store!.LightweightSession();
        session.Events.StartStream<Reservation>(Reservation.StreamIdFor(reservationId), new ReservationCreatedEvent
        {
            ReservationId = reservationId,
            Purpose = "SO-PENDING",
            Priority = 1,
            Timestamp = DateTime.UtcNow,
            RequestedLines = new List<LKvitai.MES.Contracts.Events.ReservationLineDto>
            {
                new() { SKU = "SKU-PENDING", Quantity = 10m }
            }
        });
        await session.SaveChangesAsync();
    }

    private async Task SeedReservationAsync(
        Guid reservationId,
        Guid huId,
        string warehouseId,
        string location,
        string sku,
        decimal quantity,
        ReservationStatus status)
    {
        await using var session = _store!.LightweightSession();
        var streamId = Reservation.StreamIdFor(reservationId);
        var now = DateTime.UtcNow;

        session.Events.StartStream<Reservation>(streamId, new ReservationCreatedEvent
        {
            ReservationId = reservationId,
            Purpose = "SO-TEST",
            Priority = 1,
            Timestamp = now,
            RequestedLines = new List<LKvitai.MES.Contracts.Events.ReservationLineDto>
            {
                new() { SKU = sku, Quantity = quantity }
            }
        });

        if (status is ReservationStatus.ALLOCATED or ReservationStatus.PICKING)
        {
            session.Events.Append(streamId, new StockAllocatedEvent
            {
                ReservationId = reservationId,
                Timestamp = now.AddSeconds(1),
                LockType = "SOFT",
                Allocations = new List<AllocationDto>
                {
                    new()
                    {
                        SKU = sku,
                        Quantity = quantity,
                        WarehouseId = warehouseId,
                        Location = location,
                        HandlingUnitIds = new List<Guid> { huId }
                    }
                }
            });
        }

        if (status == ReservationStatus.PICKING)
        {
            session.Events.Append(streamId, new PickingStartedEvent
            {
                ReservationId = reservationId,
                Timestamp = now.AddSeconds(2),
                LockType = "HARD",
                HardLockedLines = new List<HardLockLineDto>
                {
                    new()
                    {
                        WarehouseId = warehouseId,
                        Location = location,
                        SKU = sku,
                        HardLockedQty = quantity
                    }
                }
            });
        }

        await session.SaveChangesAsync();
    }

    private static string? GetErrorCode(object? resultValue)
    {
        var problem = resultValue as ProblemDetails;
        if (problem?.Extensions is null || !problem.Extensions.TryGetValue("errorCode", out var codeValue))
        {
            return null;
        }

        return codeValue?.ToString();
    }
}

using FluentAssertions;
using LKvitai.MES.Modules.Warehouse.Api.Controllers;
using LKvitai.MES.Modules.Warehouse.Application.Ports;
using LKvitai.MES.Modules.Warehouse.Application.Queries;
using LKvitai.MES.Contracts.Events;
using LKvitai.MES.Modules.Warehouse.Domain.Aggregates;
using LKvitai.MES.Modules.Warehouse.Infrastructure.Persistence;
using LKvitai.MES.Modules.Warehouse.Projections;
using Marten;
using Marten.Events.Projections;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;
using Xunit;

namespace LKvitai.MES.Tests.Integration;

public class ReservationsControllerIntegrationTests : IAsyncLifetime
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

            opts.Projections.Add<ReservationSummaryProjection>(ProjectionLifecycle.Async);
            opts.Projections.Add<HandlingUnitProjection>(ProjectionLifecycle.Async);
        });

        await _store.Storage.ApplyAllConfiguredChangesToDatabaseAsync();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(_store);
        services.AddScoped<IReservationRepository, MartenReservationRepository>();
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining<SearchReservationsQueryHandler>();
        });

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
    public async Task GetReservations_FilterAndPagination_ReturnsPagedFullDetails()
    {
        DockerRequirement.EnsureEnabled();

        var allocatedReservationId = Guid.NewGuid();
        var pickingReservationId = Guid.NewGuid();
        var consumedReservationId = Guid.NewGuid();

        await SeedReservationAsync(
            allocatedReservationId,
            purpose: "SO-ALLOCATED",
            createdAt: DateTime.UtcNow.AddMinutes(-3),
            status: ReservationStatus.ALLOCATED);

        await SeedReservationAsync(
            pickingReservationId,
            purpose: "SO-PICKING",
            createdAt: DateTime.UtcNow.AddMinutes(-2),
            status: ReservationStatus.PICKING);

        await SeedReservationAsync(
            consumedReservationId,
            purpose: "SO-CONSUMED",
            createdAt: DateTime.UtcNow.AddMinutes(-1),
            status: ReservationStatus.CONSUMED);

        using (var daemon = await _store!.BuildProjectionDaemonAsync())
        {
            await daemon.StartAllAsync();
            await daemon.WaitForNonStaleData(TimeSpan.FromSeconds(15));
            await daemon.StopAllAsync();
        }

        using var scope = _provider!.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var reservationRepository = scope.ServiceProvider.GetRequiredService<IReservationRepository>();
        var controller = new ReservationsController(mediator, reservationRepository)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        var allocatedPage = await controller.GetReservationsAsync(
            status: "ALLOCATED",
            page: 1,
            pageSize: 10,
            cancellationToken: CancellationToken.None);

        var allocatedResult = allocatedPage.Should().BeOfType<OkObjectResult>().Subject;
        var allocatedPayload = allocatedResult.Value.Should().BeOfType<PagedResult<ReservationDto>>().Subject;

        allocatedPayload.TotalCount.Should().Be(1);
        allocatedPayload.Items.Should().HaveCount(1);
        allocatedPayload.Items[0].ReservationId.Should().Be(allocatedReservationId);
        allocatedPayload.Items[0].Lines.Should().NotBeEmpty();
        allocatedPayload.Items[0].Lines[0].AllocatedHUs.Should().NotBeEmpty();

        var allPageFirst = await controller.GetReservationsAsync(
            status: null,
            page: 1,
            pageSize: 1,
            cancellationToken: CancellationToken.None);

        var allResult = allPageFirst.Should().BeOfType<OkObjectResult>().Subject;
        var allPayload = allResult.Value.Should().BeOfType<PagedResult<ReservationDto>>().Subject;

        allPayload.TotalCount.Should().Be(2, "null status should return active reservations only");
        allPayload.PageSize.Should().Be(1);
        allPayload.Items.Should().HaveCount(1);
    }

    private async Task SeedReservationAsync(
        Guid reservationId,
        string purpose,
        DateTime createdAt,
        ReservationStatus status)
    {
        var streamId = Reservation.StreamIdFor(reservationId);
        var huId = Guid.NewGuid();

        await using var session = _store!.LightweightSession();
        session.Events.StartStream<Reservation>(
            streamId,
            new ReservationCreatedEvent
            {
                ReservationId = reservationId,
                Purpose = purpose,
                Priority = 1,
                Timestamp = createdAt,
                RequestedLines = new List<LKvitai.MES.Contracts.Events.ReservationLineDto>
                {
                    new()
                    {
                        SKU = "SKU-001",
                        Quantity = 10m
                    }
                }
            });

        session.Events.Append(streamId, new StockAllocatedEvent
        {
            ReservationId = reservationId,
            Timestamp = createdAt.AddSeconds(1),
            LockType = "SOFT",
            Allocations = new List<AllocationDto>
            {
                new()
                {
                    SKU = "SKU-001",
                    Quantity = 10m,
                    WarehouseId = "WH1",
                    Location = "LOC-A",
                    HandlingUnitIds = new List<Guid> { huId }
                }
            }
        });

        if (status == ReservationStatus.PICKING || status == ReservationStatus.CONSUMED)
        {
            session.Events.Append(streamId, new PickingStartedEvent
            {
                ReservationId = reservationId,
                Timestamp = createdAt.AddSeconds(2),
                LockType = "HARD"
            });
        }

        if (status == ReservationStatus.CONSUMED)
        {
            session.Events.Append(streamId, new ReservationConsumedEvent
            {
                ReservationId = reservationId,
                Timestamp = createdAt.AddSeconds(3),
                ActualQuantity = 10m
            });
        }

        await session.SaveChangesAsync();
    }
}

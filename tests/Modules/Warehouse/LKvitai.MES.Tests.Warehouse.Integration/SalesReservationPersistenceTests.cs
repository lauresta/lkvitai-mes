using FluentAssertions;
using LKvitai.MES.Contracts.Events;
using LKvitai.MES.Contracts.ReadModels;
using LKvitai.MES.Modules.Warehouse.Api.Controllers;
using LKvitai.MES.Modules.Warehouse.Api.Services;
using LKvitai.MES.Modules.Warehouse.Application.Commands;
using LKvitai.MES.Modules.Warehouse.Application.Orchestration;
using LKvitai.MES.Modules.Warehouse.Domain.Entities;
using LKvitai.MES.Modules.Warehouse.Infrastructure.Persistence;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Reservation = LKvitai.MES.Modules.Warehouse.Domain.Aggregates.Reservation;

namespace LKvitai.MES.Tests.Warehouse.Integration;

public partial class PickingWorkflowIntegrationTests
{
    private SalesOrderReservationService Reservations(WarehouseDbContext db)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(db);
        services.AddSingleton(_store!);
        services.AddScoped<IAllocateReservationOrchestration, MartenAllocateReservationOrchestration>();
        services.AddMediatR(c => c.RegisterServicesFromAssemblyContaining<AllocateReservationCommandHandler>());
        var provider = services.BuildServiceProvider();
        return new SalesOrderReservationService(db, _store!, provider.GetRequiredService<IMediator>(), new QuietBus());
    }

    private static async Task<SalesOrder> Draft(WarehouseDbContext db)
    {
        var code = "QA-C3-" + Guid.NewGuid().ToString("N");
        var sale = new SalesOrder { Customer = new Customer { CustomerCode = code, Name = code } };
        sale.Lines.Add(new SalesOrderLine { ItemId = 1, OrderedQty = 5, UnitPrice = 10 });
        db.SalesOrders.Add(sale);
        await db.SaveChangesAsync();
        return await db.SalesOrders.Include(x => x.Lines).ThenInclude(x => x.Item).SingleAsync(x => x.Id == sale.Id);
    }

    [SkippableFact]
    public async Task SalesReserve_UsesCommittedReservationsDespiteProjectionLag_CancelReleasesAndRetryAllocates()
    {
        DockerRequirement.EnsureEnabled();
        await SeedBaseDataAsync();
        await SeedAvailableStockAsync(5);
        await using var db = CreateDbContext();
        var first = await Draft(db);
        var service = Reservations(db);
        (await service.AllocateAsync(first, () => first.Allocate(first.Id), default)).IsSuccess.Should().BeTrue();
        var second = await Draft(db);
        (await service.AllocateAsync(second, () => second.Allocate(second.Id), default)).IsSuccess.Should().BeTrue();
        second.Status.Should().Be(SalesOrderStatus.PendingStock);
        await using var query = _store!.QuerySession();
        (await query.Events.FetchStreamAsync(Reservation.StreamIdFor(second.Id))).Should().BeEmpty();
        var allocations = (await query.Events.FetchStreamAsync(Reservation.StreamIdFor(first.Id)))
            .Select(x => x.Data).OfType<StockAllocatedEvent>().Single();
        allocations.SalesOrderReservation.Should().BeTrue();
        allocations.Allocations.Should().ContainSingle().Which.Quantity.Should().Be(5);
        var projection = new LKvitai.MES.Modules.Warehouse.Projections.AvailableStockProjection();
        var stock = new AvailableStockView { Id = AvailableStockView.ComputeId("WH1", "A-01", "RM-0001"), OnHandQty = 5 };
        projection.Apply(allocations, stock).AvailableQty.Should().Be(0);
        var cancel = new CancelSalesOrderCommandHandler(db, new QuietBus(), new StaticCurrentUserService("QA-C3"), _store!);
        (await cancel.Handle(new CancelSalesOrderCommand { SalesOrderId = first.Id, Reason = "QA-C3-release" }, default))
            .IsSuccess.Should().BeTrue();
        var release = (await query.Events.FetchStreamAsync(Reservation.StreamIdFor(first.Id))).Select(x => x.Data)
            .OfType<ReservationCancelledEvent>().Single();
        projection.Apply(release, stock).AvailableQty.Should().Be(5);
        (await service.AllocateAsync(second, () => second.Allocate(second.Id), default)).IsSuccess.Should().BeTrue();
        second.Status.Should().Be(SalesOrderStatus.Allocated);
    }

    [SkippableFact]
    public async Task SalesReserve_RejectsVirtualStock_AndReleaseCreatesRealTasksWithPartialConsumption()
    {
        DockerRequirement.EnsureEnabled();
        await SeedBaseDataAsync();
        await SeedAvailableStockAsync(0);
        await using (var session = _store!.LightweightSession())
        {
            session.Store(new AvailableStockView { Id = AvailableStockView.ComputeId("WH1", "SHIPPING", "RM-0001"),
                WarehouseId = "WH1", Location = "SHIPPING", SKU = "RM-0001", OnHandQty = 100, AvailableQty = 100 });
            await session.SaveChangesAsync();
        }
        await using var db = CreateDbContext();
        var sale = await Draft(db);
        var service = Reservations(db);
        (await service.AllocateAsync(sale, () => sale.Allocate(sale.Id), default)).IsSuccess.Should().BeTrue();
        sale.Status.Should().Be(SalesOrderStatus.PendingStock);
        await SeedAvailableStockAsync(5);
        (await service.AllocateAsync(sale, () => sale.Allocate(sale.Id), default)).IsSuccess.Should().BeTrue();
        var release = new ReleaseSalesOrderCommandHandler(db, new QuietBus());
        (await release.Handle(new ReleaseSalesOrderCommand { SalesOrderId = sale.Id }, default)).IsSuccess.Should().BeTrue();
        var task = await db.PickTasks.SingleAsync();
        task.OrderId.Should().Be(sale.OutboundOrderId!.Value.ToString());
        var suggested = (OkObjectResult)await CreateController(db).GetLocationSuggestionsAsync(task.TaskId);
        var locations = (PickingController.PickLocationSuggestionResponse)suggested.Value!;
        locations.Locations.Should().ContainSingle().Which.AvailableQty.Should().Be(5);

        (await CreateController(db).CompleteTaskAsync(task.TaskId,
            new PickingController.CompletePickTaskRequest(2, 2, null, "BAR-ITEM-1", "LOC-A01", "QA-C3-partial")))
            .Should().BeOfType<OkObjectResult>();
        await using var query = _store!.QuerySession();
        var events = await query.Events.FetchStreamAsync(Reservation.StreamIdFor(sale.Id));
        var consumed = events.Select(x => x.Data).OfType<ReservationConsumedEvent>().Single();
        consumed.Completed.Should().BeFalse();
        consumed.ReleasedSoftLines.Single().Quantity.Should().Be(2);
        SalesReservationBalance.Remaining(events.Select(x => x.Data)).Sum(x => x.Quantity).Should().Be(3);
        var remainder = await db.PickTasks.SingleAsync(x => x.Status == "Pending");
        (await CreateController(db).CompleteTaskAsync(remainder.TaskId,
            new PickingController.CompletePickTaskRequest(2, 3, null, "BAR-ITEM-1", "LOC-A01", "QA-C3-rest")))
            .Should().BeOfType<OkObjectResult>();
        var final = await query.Events.FetchStreamAsync(Reservation.StreamIdFor(sale.Id));
        final.Select(x => x.Data).OfType<ReservationConsumedEvent>().Last().Completed.Should().BeTrue();
        SalesReservationBalance.Remaining(final.Select(x => x.Data)).Should().BeEmpty();
    }
    [SkippableFact]
    public async Task SalesReserve_ConcurrentRequestsCannotReserveTheSameStockTwice()
    {
        DockerRequirement.EnsureEnabled();
        await SeedBaseDataAsync();
        await SeedAvailableStockAsync(5);
        await using var firstDb = CreateDbContext();
        await using var secondDb = CreateDbContext();
        var first = await Draft(firstDb);
        var second = await Draft(secondDb);
        var results = await Task.WhenAll(
            Reservations(firstDb).AllocateAsync(first, () => first.Allocate(first.Id), default),
            Reservations(secondDb).AllocateAsync(second, () => second.Allocate(second.Id), default));
        results.Should().OnlyContain(x => x.IsSuccess);
        new[] { first.Status, second.Status }.Should().BeEquivalentTo(new[] { SalesOrderStatus.Allocated, SalesOrderStatus.PendingStock });
        await using var query = _store!.QuerySession();
        var events = (await query.Events.FetchStreamAsync(Reservation.StreamIdFor(first.Id)))
            .Concat(await query.Events.FetchStreamAsync(Reservation.StreamIdFor(second.Id)));
        events.Select(x => x.Data).OfType<StockAllocatedEvent>().Should().ContainSingle()
            .Which.Allocations.Sum(x => x.Quantity).Should().Be(5);
    }

    [SkippableFact]
    public async Task SalesReserve_EfFailureRollsBackTheMartenReservation()
    {
        DockerRequirement.EnsureEnabled();
        await SeedBaseDataAsync();
        await SeedAvailableStockAsync(5);
        await using var db = CreateDbContext();
        var sale = await Draft(db);
        await db.Database.ExecuteSqlRawAsync("""
            CREATE FUNCTION public.qa_reject_sales_update() RETURNS trigger AS $$
            BEGIN RAISE EXCEPTION 'QA sales save failure'; END; $$ LANGUAGE plpgsql;
            CREATE TRIGGER qa_reject_sales_update BEFORE UPDATE ON public.sales_orders
            FOR EACH ROW EXECUTE FUNCTION public.qa_reject_sales_update();
            """);
        var action = async () => await Reservations(db).AllocateAsync(sale, () => sale.Allocate(sale.Id), default);
        await action.Should().ThrowAsync<Exception>();
        await using var verify = CreateDbContext();
        (await verify.SalesOrders.SingleAsync()).Status.Should().Be(SalesOrderStatus.Draft);
        await using var query = _store!.QuerySession();
        (await query.Events.FetchStreamAsync(Reservation.StreamIdFor(sale.Id))).Should().BeEmpty();
    }

}

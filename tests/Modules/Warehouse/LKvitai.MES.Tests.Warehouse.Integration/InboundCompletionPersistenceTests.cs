using FluentAssertions;
using LKvitai.MES.Modules.Warehouse.Api.Controllers;
using LKvitai.MES.Contracts.Events;
using LKvitai.MES.Modules.Warehouse.Domain.Entities;
using LKvitai.MES.Modules.Warehouse.Infrastructure.Pricing;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace LKvitai.MES.Tests.Warehouse.Integration;

public partial class ReceivingWorkflowIntegrationTests
{
    [SkippableFact]
    public async Task ValuationQuantity_ReadsHistoricalAliasesAndCaseInsensitiveSku()
    {
        DockerRequirement.EnsureEnabled();
        await SeedBaseDataAsync(false, false);
        await using var db = CreateDbContext();
        db.Locations.Add(new Location { Id = 4, Code = "A-01", Barcode = "A-01", IsVirtual = false, Type = "Bin", Status = "Active" });
        await db.SaveChangesAsync();
        await using (var session = _store!.LightweightSession())
        {
            session.Events.Append("QA-alias-receipts",
                new GoodsReceivedEvent { WarehouseId = "WH1", SKU = "RM-0001", DestinationLocation = "A-01", ReceivedQty = 1 },
                new GoodsReceivedEvent { WarehouseId = "WH1", SKU = "rm-0001", DestinationLocation = "A-01", ReceivedQty = 2 });
            await session.SaveChangesAsync();
        }
        var resolver = new LKvitai.MES.Modules.Warehouse.Api.Services.MartenAvailableStockQuantityResolver(_store!, db);
        (await resolver.ResolveTotalQtyAsync(1, "RM-0001", default)).Should().Be(3);
    }

    [SkippableFact]
    public async Task CloseShort_PersistsStatusWithoutChangingStock_AndBlocksAnotherReceipt()
    {
        DockerRequirement.EnsureEnabled();
        await SeedBaseDataAsync(false, false);
        await using var db = CreateDbContext();
        var shipment = await CreateShipmentAsync(db, "QA-C4-close", 1, 1, 10);
        var controller = CreateReceivingController(db);
        (await controller.ReceiveGoodsAsync(shipment.Id, new(shipment.Lines.Single().Id, 4, null, null, null, "QA-C4", null, null)))
            .Should().BeOfType<OkObjectResult>();
        (await controller.CloseShortAsync(shipment.Id, new("QA-C4-short"))).Should().BeOfType<OkObjectResult>();
        await using var verify = CreateDbContext();
        (await verify.InboundShipments.SingleAsync()).Status.Should().Be("Complete");
        (await verify.InboundShipmentLines.SingleAsync()).ReceivedQty.Should().Be(4);
        await using var query = _store!.QuerySession();
        var events = await query.Events.FetchStreamAsync(ShipmentStreamId(shipment.Id));
        events.Select(x => x.Data).OfType<GoodsReceivedEvent>().Should().ContainSingle().Which.ReceivedQty.Should().Be(4);
        events.Select(x => x.Data).OfType<InboundShipmentClosedEvent>().Should().ContainSingle();
        (await controller.ReceiveGoodsAsync(shipment.Id, new(shipment.Lines.Single().Id, 1, null, null, null, "QA-C4-repeat", null, null)))
            .Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(400);
    }

    [SkippableFact]
    public async Task Receipt_MartenFailureRollsBackEfQuantityAndLot()
    {
        DockerRequirement.EnsureEnabled();
        await SeedBaseDataAsync(true, false);
        await using var db = CreateDbContext();
        var shipment = await CreateShipmentAsync(db, "QA-C4-rollback", 1, 1, 10);
        await db.Database.ExecuteSqlRawAsync("""
            CREATE FUNCTION public.qa_reject_receipt() RETURNS trigger AS $$
            BEGIN RAISE EXCEPTION 'QA receipt event failure'; END; $$ LANGUAGE plpgsql;
            CREATE TRIGGER qa_reject_receipt BEFORE INSERT ON warehouse_events.mt_events
            FOR EACH ROW EXECUTE FUNCTION public.qa_reject_receipt();
            """);
        var action = async () => await CreateReceivingController(db).ReceiveGoodsAsync(shipment.Id,
            new(shipment.Lines.Single().Id, 4, "QA-C4-LOT", null, null, "QA-C4-rollback", null, null));
        await action.Should().ThrowAsync<Exception>();
        await using var verify = CreateDbContext();
        (await verify.InboundShipmentLines.SingleAsync()).ReceivedQty.Should().Be(0);
        (await verify.Lots.CountAsync()).Should().Be(0);
        await using var query = _store!.QuerySession();
        (await query.Events.FetchStreamAsync(ShipmentStreamId(shipment.Id))).Should().BeEmpty();
    }

    [SkippableFact]
    public async Task Receipt_ValuationFailureIsReturnedAfterTheAtomicReceipt()
    {
        DockerRequirement.EnsureEnabled();
        await SeedBaseDataAsync(false, false);
        await using var db = CreateDbContext();
        var shipment = await CreateShipmentAsync(db, "QA-C4-WAC", 1, 1, 10);
        var controller = CreateReceivingController(db, out var mediator);
        mediator.Fail = true;
        var result = await controller.ReceiveGoodsAsync(shipment.Id,
            new(shipment.Lines.Single().Id, 4, null, null, null, "QA-C4-WAC", null, null));
        result.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(500);
        await using var query = _store!.QuerySession();
        (await query.Events.FetchStreamAsync(ShipmentStreamId(shipment.Id))).Select(x => x.Data)
            .OfType<GoodsReceivedEvent>().Should().ContainSingle().Which.ReceivedQty.Should().Be(4);
        await using var verify = CreateDbContext();
        (await verify.InboundShipmentLines.SingleAsync()).ReceivedQty.Should().Be(4);
    }

    [SkippableTheory]
    [InlineData("RECEIVING", true)]
    [InlineData("RECEIVING", false)]
    [InlineData("RETURN_TO_SUPPLIER", true)]
    [InlineData("RETURN_TO_SUPPLIER", false)]
    [InlineData("SCRAP", true)]
    [InlineData("SCRAP", false)]
    public async Task Quarantine_DispositionPersistsMovement_AndCannotSpendItTwice(string destination, bool adjustment)
    {
        DockerRequirement.EnsureEnabled();
        await SeedBaseDataAsync(false, true);
        await using var db = CreateDbContext();
        if (destination != "RECEIVING")
        {
            db.Locations.Add(new Location { Id = 4, Code = destination, Barcode = "QA-C4-" + destination,
                IsVirtual = true, Type = "Zone", Status = "Active" });
            await db.SaveChangesAsync();
        }
        await using (var seed = _store!.LightweightSession())
        {
            if (adjustment) seed.Events.Append("QA-C4-quarantine", new StockAdjustedEvent { WarehouseId = "WH1", SKU = "RM-0001", Location = "QUARANTINE", QtyDelta = 5 });
            else seed.Events.Append("QA-C4-quarantine", new GoodsReceivedEvent { WarehouseId = "WH1", SKU = "RM-0001", DestinationLocation = "QC_HOLD", ReceivedQty = 5 },
                new QCFailedEvent { WarehouseId = "WH1", SKU = "RM-0001", FromLocation = "QC_HOLD", ToLocation = "QUARANTINE", Qty = 5 });
            await seed.SaveChangesAsync();
        }
        var controller = CreateQcController(db);
        (await controller.DisposeQuarantineAsync(new(1, 5, destination, "QA-C4-dispose"))).Should().BeOfType<OkObjectResult>();
        (await controller.DisposeQuarantineAsync(new(1, 5, destination, "QA-C4-repeat"))).Should().BeOfType<ObjectResult>()
            .Which.StatusCode.Should().Be(422);
        await using var query = _store!.QuerySession();
        // Stream ID is derived by the production helper; verify the actual movement and its projection input.
        var all = await LKvitai.MES.Modules.Warehouse.Infrastructure.Persistence.CommittedEventData.ReadAsync<StockMovedEvent>(query, "RM-0001", default);
        all.Should().ContainSingle().Which.ToLocation.Should().Be(destination);
        var rows = new object[] { new GoodsReceivedEvent { SKU = "RM-0001", DestinationLocation = "QUARANTINE", ReceivedQty = 5 }, all.Single() };
        StockEventQuantity.CalculateAtLocation(rows, "RM-0001", "QUARANTINE").Should().Be(0);
        StockEventQuantity.CalculateAtLocation(rows, "RM-0001", destination).Should().Be(5);
    }
}

public partial class PutawayWorkflowIntegrationTests
{
    [SkippableFact]
    public async Task ConcurrentPutaway_UsesCommittedReceiptsDespiteStaleProjection()
    {
        DockerRequirement.EnsureEnabled();
        await SeedBaseDataAsync();
        await SeedStockProjectionAsync(10, 0);
        await using var first = CreateDbContext();
        await using var second = CreateDbContext();
        var results = await Task.WhenAll(
            CreatePutawayController(first).PutawayAsync(new(1, 8, 1, 2, null, "QA-C4-first")),
            CreatePutawayController(second).PutawayAsync(new(1, 8, 1, 2, null, "QA-C4-second")));
        results.OfType<OkObjectResult>().Should().ContainSingle();
        results.OfType<ObjectResult>().Count(x => x.StatusCode == 422).Should().Be(1);
        await using var query = _store!.QuerySession();
        var moves = await LKvitai.MES.Modules.Warehouse.Infrastructure.Persistence.CommittedEventData.ReadAsync<StockMovedEvent>(query, "RM-0001", default);
        moves.Should().ContainSingle().Which.Quantity.Should().Be(8);
        var receipts = await LKvitai.MES.Modules.Warehouse.Infrastructure.Persistence.CommittedEventData.ReadAsync<GoodsReceivedEvent>(query, "RM-0001", default);
        StockEventQuantity.CalculateAtLocation(receipts.Cast<object>().Concat(moves), "RM-0001", "RECEIVING").Should().Be(2);
    }
}

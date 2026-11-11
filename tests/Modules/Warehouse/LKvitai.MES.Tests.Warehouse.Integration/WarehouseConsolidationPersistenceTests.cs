using FluentAssertions;
using LKvitai.MES.Contracts.Events;
using LKvitai.MES.Contracts.ReadModels;
using LKvitai.MES.Modules.Warehouse.Api.Configuration;
using LKvitai.MES.Modules.Warehouse.Api.Services;
using LKvitai.MES.Modules.Warehouse.Application.Commands;
using LKvitai.MES.Modules.Warehouse.Application.Projections;
using LKvitai.MES.Modules.Warehouse.Domain.Entities;
using LKvitai.MES.Modules.Warehouse.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace LKvitai.MES.Tests.Warehouse.Integration;

// C8c: one warehouse code, consolidation of stock recorded under old codes.
public partial class ReceivingWorkflowIntegrationTests
{
    private static readonly ProjectionAuditActor ConsolidationActor = new("QA-C8c-admin", "127.0.0.1", "QA-C8c-test");

    private async Task SeedConsolidationBaseAsync()
    {
        await SeedBaseDataAsync(false, false);
        await using var db = CreateDbContext();
        db.Locations.AddRange(
            new Location { Id = 10, Code = "A-01", Barcode = "A-01", Type = "Bin", IsVirtual = false, Status = "Active" },
            new Location { Id = 11, Code = "A-02", Barcode = "A-02", Type = "Bin", IsVirtual = false, Status = "Active" },
            new Location { Id = 12, Code = "A-03", Barcode = "A-03", Type = "Bin", IsVirtual = false, Status = "Active" },
            new Location { Id = 13, Code = "WAREHOUSE_MERGE", Barcode = "VIRTUAL-WM", Type = "Zone", IsVirtual = true, Status = "Active" });
        await db.SaveChangesAsync();
    }

    // Written exactly like goods receipt / Agnum distribution: the warehouse exists only in the stream id.
    private async Task AppendReceiptAsync(string warehouse, string cell, decimal qty, string from = "AGNUM")
    {
        await using var session = _store!.LightweightSession();
        session.Events.Append($"stock-ledger:{warehouse}:{cell}:RM-0001", new StockMovedEvent
        {
            MovementId = Guid.NewGuid(), SKU = "RM-0001", Quantity = qty, FromLocation = from, ToLocation = cell,
            MovementType = "RECEIPT", Reason = "QA-C8c seed"
        });
        await session.SaveChangesAsync();
    }

    private WarehouseConsolidationService Consolidation(WarehouseDbContext db, string code)
        => new(db, _store!, BalanceLocks(), WarehouseCodeProvider.For(code));

    private async Task<decimal> PhysicalTotalAsync()
    {
        await using var session = _store!.QuerySession();
        var balances = await CommittedLocationStock.ReadBalancesAsync(session, "RM-0001", default);
        var physical = new[] { "A-01", "A-02", "A-03" };
        return balances.Where(x => physical.Contains(x.Location)).Sum(x => x.Quantity);
    }

    [SkippableFact]
    public async Task Consolidation_MovesStockToConfiguredCode_RepeatIsEmpty()
    {
        DockerRequirement.EnsureEnabled();
        await SeedConsolidationBaseAsync();
        await AppendReceiptAsync("W1", "A-01", 5);
        await AppendReceiptAsync("1", "A-02", 2);
        await AppendReceiptAsync("W1", "SHIPPING", 3, "SUPPLIER");
        var before = await PhysicalTotalAsync();
        before.Should().Be(7m); // physical cells only: 5 under W1 + 2 under 1
        await using var db = CreateDbContext();
        var service = Consolidation(db, "1");

        var preview = await service.PreviewAsync();

        preview.TargetWarehouse.Should().Be("1");
        preview.Rows.Should().Contain(x => x.Warehouse == "W1" && x.Location == "A-01" && x.Quantity == 5m && x.Status == "Movable");
        // Virtual balances under the old code are shown but never moved; the configured code is not listed at all.
        preview.Rows.Should().Contain(x => x.Location == "SHIPPING" && x.Status == "Virtual");
        preview.Rows.Should().NotContain(x => x.Warehouse == "1");

        var result = await service.ApplyAsync("QA-C8c merge W1", ConsolidationActor);

        result.Moved.Should().ContainSingle().Which.Location.Should().Be("A-01");
        await using (var query = _store!.QuerySession())
        {
            var moves = (await CommittedEventData.ReadStockMovedAsync(query, "RM-0001", default))
                .Where(x => x.Event.Reason!.StartsWith("Warehouse consolidation")).ToList();
            moves.Should().HaveCount(2);
            moves.Should().Contain(x => x.Warehouse == "W1" && x.Event.FromLocation == "A-01" && x.Event.ToLocation == "WAREHOUSE_MERGE" && x.Event.Quantity == 5m);
            moves.Should().Contain(x => x.Warehouse == "1" && x.Event.FromLocation == "WAREHOUSE_MERGE" && x.Event.ToLocation == "A-01" && x.Event.Quantity == 5m);
            moves.Select(x => x.Event.MovementId).Distinct().Should().HaveCount(2).And.NotContain(Guid.Empty);
        }
        (await CommittedLocationStock.ReadAsync(_store!, "W1", "RM-0001", "A-01", default)).Should().Be(0m);
        (await CommittedLocationStock.ReadAsync(_store!, "1", "RM-0001", "A-01", default)).Should().Be(5m);
        (await CommittedLocationStock.ReadAsync(_store!, "1", "RM-0001", "A-02", default)).Should().Be(2m);
        (await PhysicalTotalAsync()).Should().Be(before); // the physical total per SKU is unchanged
        await using var verify = CreateDbContext();
        var audit = await verify.SecurityAuditLogs.SingleAsync(x => x.Action == "WarehouseConsolidationApplied");
        audit.UserId.Should().Be("QA-C8c-admin");
        audit.Details.Should().Contain("QA-C8c merge W1").And.Contain("\"rowCount\":1");

        // A repeat adds nothing: no events, no audit record.
        var repeat = await Consolidation(verify, "1").ApplyAsync("QA-C8c repeat", ConsolidationActor);
        repeat.Moved.Should().BeEmpty();
        await using var again = _store!.QuerySession();
        (await CommittedEventData.ReadStockMovedAsync(again, "RM-0001", default)).Count(x => x.Event.Reason!.StartsWith("Warehouse consolidation")).Should().Be(2);
        (await verify.SecurityAuditLogs.CountAsync(x => x.Action == "WarehouseConsolidationApplied")).Should().Be(1);
        (await Consolidation(verify, "1").PreviewAsync()).Rows.Should().NotContain(x => x.Status == "Movable");
    }

    [SkippableFact]
    public async Task Consolidation_ReservationAndHardLockBlockTheirRows_OthersAreMoved()
    {
        DockerRequirement.EnsureEnabled();
        await SeedConsolidationBaseAsync();
        await AppendReceiptAsync("W1", "A-01", 5);
        await AppendReceiptAsync("W1", "A-02", 3);
        await AppendReceiptAsync("W1", "A-03", 4);
        await using (var session = _store!.LightweightSession())
        {
            session.Store(new AvailableStockView
            {
                Id = AvailableStockView.ComputeId("W1", "A-01", "RM-0001"), WarehouseId = "W1", Location = "A-01", SKU = "RM-0001",
                OnHandQty = 5, ReservedQty = 1, AvailableQty = 4, LastUpdated = DateTime.UtcNow
            });
            session.Store(new ActiveHardLockView
            {
                Id = ActiveHardLockView.ComputeId(Guid.NewGuid(), "A-03", "RM-0001"), WarehouseId = "W1",
                ReservationId = Guid.NewGuid(), Location = "A-03", SKU = "RM-0001", HardLockedQty = 4, StartedAt = DateTime.UtcNow
            });
            await session.SaveChangesAsync();
        }
        await using var db = CreateDbContext();
        var service = Consolidation(db, "1");

        var preview = await service.PreviewAsync();
        preview.Rows.Single(x => x.Location == "A-01").Should().Match<ConsolidationRow>(x => x.Status == "Blocked" && x.Reason == "Reservation");
        preview.Rows.Single(x => x.Location == "A-03").Should().Match<ConsolidationRow>(x => x.Status == "Blocked" && x.Reason == "HardLock");
        preview.Rows.Single(x => x.Location == "A-02").Status.Should().Be("Movable");

        var result = await service.ApplyAsync("QA-C8c blocked rows", ConsolidationActor);

        result.Moved.Should().ContainSingle().Which.Location.Should().Be("A-02");
        (await CommittedLocationStock.ReadAsync(_store!, "W1", "RM-0001", "A-01", default)).Should().Be(5m);
        (await CommittedLocationStock.ReadAsync(_store!, "W1", "RM-0001", "A-03", default)).Should().Be(4m);
        (await CommittedLocationStock.ReadAsync(_store!, "W1", "RM-0001", "A-02", default)).Should().Be(0m);
        (await CommittedLocationStock.ReadAsync(_store!, "1", "RM-0001", "A-02", default)).Should().Be(3m);
        await using var query = _store!.QuerySession();
        (await CommittedEventData.ReadStockMovedAsync(query, "RM-0001", default))
            .Count(x => x.Event.Reason!.StartsWith("Warehouse consolidation")).Should().Be(2);
    }

    [SkippableFact]
    public async Task StockRead_SeesGoodsReceiptEventsWhoseWarehouseIsOnlyInTheStreamId()
    {
        DockerRequirement.EnsureEnabled();
        await SeedConsolidationBaseAsync();
        // The real receipt path (also used by "Paskirstyti"): StockMoved carries no warehouse in its data.
        var receive = await new MartenReceiveGoodsOrchestration(_store!, NullLogger<MartenReceiveGoodsOrchestration>.Instance)
            .ExecuteAsync(new ReceiveGoodsCommand
            {
                WarehouseId = "1", Location = "A-01", FromLocation = "AGNUM",
                Lines = [new ReceiveGoodsLineDto { SKU = "RM-0001", Quantity = 6m }]
            }, default);
        receive.IsSuccess.Should().BeTrue();

        await using var query = _store!.QuerySession();
        var stored = await CommittedEventData.ReadStockMovedAsync(query, "RM-0001", default);
        stored.Should().ContainSingle().Which.Warehouse.Should().Be("1");
        (await CommittedEventData.ReadAsync<StockMovedEvent>(query, "RM-0001", default)).Should().ContainSingle(); // data has no warehouse field

        (await CommittedLocationStock.ReadAsync(_store!, "1", "RM-0001", "A-01", default)).Should().Be(6m);
        (await CommittedLocationStock.ReadAsync(_store!, "WH1", "RM-0001", "A-01", default)).Should().Be(0m);
        (await CommittedLocationStock.ReadAsync(_store!, "1", "RM-0001", "AGNUM", default)).Should().Be(-6m);
    }
}

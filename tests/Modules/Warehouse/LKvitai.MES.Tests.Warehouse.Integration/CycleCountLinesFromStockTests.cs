using FluentAssertions;
using LKvitai.MES.Contracts.Events;
using LKvitai.MES.Modules.Warehouse.Api.Configuration;
using LKvitai.MES.Modules.Warehouse.Api.Services;
using LKvitai.MES.Modules.Warehouse.Application.Commands;
using LKvitai.MES.Modules.Warehouse.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace LKvitai.MES.Tests.Warehouse.Integration;

// C8e: cycle count lines come from the committed physical balance per (location, SKU) of the configured warehouse.
public partial class ReceivingWorkflowIntegrationTests
{
    private async Task AppendStockAsync(string warehouse, string cell, string sku, decimal qty)
    {
        await using var session = _store!.LightweightSession();
        session.Events.Append($"stock-ledger:{warehouse}:{cell}:{sku}", new StockMovedEvent
        {
            MovementId = Guid.NewGuid(), SKU = sku, Quantity = qty, FromLocation = "AGNUM", ToLocation = cell,
            MovementType = "RECEIPT", Reason = "QA-C8e seed"
        });
        await session.SaveChangesAsync();
    }

    [SkippableFact]
    public async Task CycleCountSchedule_CreatesLinesOnlyForStockedLocationSkuPairsOfConfiguredWarehouse()
    {
        DockerRequirement.EnsureEnabled();
        await SeedConsolidationBaseAsync();
        await using (var seed = CreateDbContext())
        {
            seed.Items.Add(new Item { Id = 2, InternalSKU = "1514251720", Name = "Real fabric", CategoryId = 1, BaseUoM = "PCS", Status = "Active" });
            await seed.SaveChangesAsync();
        }

        await AppendStockAsync("1", "A-01", "1514251720", 12.5m);
        await AppendStockAsync("1", "A-01", "RM-0001", 4m);
        await AppendStockAsync("1", "A-02", "RM-0001", 6m);
        await AppendStockAsync("1", "A-02", "1514251720", 3m);
        await AppendStockAsync("1", "A-02", "1514251720", -3m);   // nets to zero: no line
        await AppendStockAsync("W1", "A-03", "RM-0001", 9m);       // other warehouse code: ignored

        await using var db = CreateDbContext();
        var resolver = new MartenCycleCountQuantityResolver(_store!, WarehouseCodeProvider.For("1"));
        var handler = new ScheduleCycleCountCommandHandler(db, new NoOpEventBus(), resolver, NullLogger<ScheduleCycleCountCommandHandler>.Instance);

        var result = await handler.Handle(new ScheduleCycleCountCommand
        {
            CommandId = Guid.NewGuid(),
            ScheduledDate = DateTimeOffset.UtcNow.AddDays(1),
            AbcClass = "ALL",
            AssignedOperator = "QA-C8e",
            LocationIds = [10, 11, 12]
        }, CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        var lines = (await db.CycleCounts.Include(x => x.Lines).SingleAsync()).Lines.ToList();
        var codes = await db.Locations.ToDictionaryAsync(x => x.Id, x => x.Code);
        var skus = await db.Items.ToDictionaryAsync(x => x.Id, x => x.InternalSKU);
        lines.Select(x => (codes[x.LocationId], skus[x.ItemId], x.SystemQty)).Should().BeEquivalentTo(new[]
        {
            ("A-01", "1514251720", 12.5m),
            ("A-01", "RM-0001", 4m),
            ("A-02", "RM-0001", 6m)
        });
    }
}

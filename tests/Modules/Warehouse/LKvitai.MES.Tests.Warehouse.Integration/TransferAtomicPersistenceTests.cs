using FluentAssertions;
using LKvitai.MES.Contracts.Events;
using LKvitai.MES.Modules.Warehouse.Api.Services;
using LKvitai.MES.Modules.Warehouse.Application.Commands;
using LKvitai.MES.Modules.Warehouse.Domain.Entities;
using LKvitai.MES.Modules.Warehouse.Infrastructure.Persistence;
using LKvitai.MES.Modules.Warehouse.Infrastructure.Pricing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace LKvitai.MES.Tests.Warehouse.Integration;

public partial class PickingWorkflowIntegrationTests
{
    [SkippableTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Transfer_BothLegsAndEfStateCommitTogether_OrSecondLegFailureRollsBack(bool failSecondLeg)
    {
        DockerRequirement.EnsureEnabled();
        await SeedBaseDataAsync();
        await SeedAvailableStockAsync(5);
        await using var db = CreateDbContext();
        await db.Database.ExecuteSqlRawAsync("SELECT setval(pg_get_serial_sequence('locations', 'Id'), 100)");
        var transfer = new Transfer { TransferNumber = "QA-C5-atomic", FromWarehouse = "WH1", ToWarehouse = "WH2", RequestedBy = "QA-C5" };
        transfer.EnsureRequestedState();
        transfer.Submit(Guid.NewGuid(), DateTimeOffset.UtcNow).IsSuccess.Should().BeTrue();
        transfer.Lines.Add(new TransferLine { ItemId = 1, Qty = 2, FromLocationId = 2, ToLocationId = 3 });
        db.Transfers.Add(transfer);
        await db.SaveChangesAsync();
        if (failSecondLeg)
            await db.Database.ExecuteSqlRawAsync("""
                CREATE FUNCTION public.qa_reject_second_leg() RETURNS trigger AS $$
                BEGIN
                    IF NEW.data->>'FromLocation' LIKE 'IN_TRANSIT_%' THEN
                        RAISE EXCEPTION 'QA-C5 reject second leg';
                    END IF;
                    RETURN NEW;
                END; $$ LANGUAGE plpgsql;
                CREATE TRIGGER qa_reject_second_leg BEFORE INSERT ON warehouse_events.mt_events
                FOR EACH ROW EXECUTE FUNCTION public.qa_reject_second_leg();
                """);
        var handler = new ExecuteTransferCommandHandler(db, new QuietBus(), new MartenTransferStockAvailabilityService(_store!),
            _store!, new StaticCurrentUserService("QA-C5"), NullLogger<ExecuteTransferCommandHandler>.Instance);
        var execute = async () => await handler.Handle(new ExecuteTransferCommand { TransferId = transfer.Id, CommandId = Guid.NewGuid() }, default);
        if (failSecondLeg) await execute.Should().ThrowAsync<Exception>();
        else (await execute()).IsSuccess.Should().BeTrue();
        await using var verify = CreateDbContext();
        (await verify.Transfers.SingleAsync()).Status.Should().Be(failSecondLeg ? TransferStatus.Approved : TransferStatus.Completed);
        (await verify.Locations.AnyAsync(x => x.Code == "IN_TRANSIT_QA-C5-atomic")).Should().Be(!failSecondLeg);
        await using var query = _store!.QuerySession();
        var moves = await CommittedEventData.ReadAsync<StockMovedEvent>(query, "RM-0001", default);
        moves.Should().HaveCount(failSecondLeg ? 0 : 2);
        if (!failSecondLeg)
        {
            var stock = new object[] { new GoodsReceivedEvent { SKU = "RM-0001", DestinationLocation = "A-01", ReceivedQty = 5 } }.Concat(moves);
            StockEventQuantity.CalculateAtLocation(stock, "RM-0001", "A-01").Should().Be(3);
            StockEventQuantity.CalculateAtLocation(stock, "RM-0001", "A-02").Should().Be(2);
            StockEventQuantity.CalculateAtLocation(stock, "RM-0001", "IN_TRANSIT_QA-C5-atomic").Should().Be(0);
            (await execute()).IsSuccess.Should().BeFalse();
            (await CommittedEventData.ReadAsync<StockMovedEvent>(query, "RM-0001", default)).Should().HaveCount(2);
        }
    }
}

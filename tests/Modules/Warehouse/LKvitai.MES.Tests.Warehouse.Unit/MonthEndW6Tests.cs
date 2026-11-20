using FluentAssertions;
using LKvitai.MES.Contracts.Events;
using LKvitai.MES.Modules.Warehouse.Domain.Aggregates;
using LKvitai.MES.Modules.Warehouse.Domain.Entities;
using LKvitai.MES.Modules.Warehouse.Infrastructure.MonthEnd;
using LKvitai.MES.Modules.Warehouse.Infrastructure.Persistence;
using LKvitai.MES.Modules.Warehouse.Infrastructure.Purchasing;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace LKvitai.MES.Tests.Warehouse.Unit;

/// <summary>Blueprint stage W6: month-end statement, bridge to Agnum, close and freeze.</summary>
public sealed class MonthEndW6Tests
{
    private sealed class TestTime : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2026, 11, 20, 10, 0, 0, TimeSpan.Zero);
    }

    private sealed class FakeSource : IStockValueEventSource
    {
        public List<StockValueChanged> Events { get; } = new();
        public Task<IReadOnlyList<StockValueChanged>> LoadAsync(DateOnly upTo, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<StockValueChanged>>(Events.Where(e => DateOnly.FromDateTime(e.BusinessDate) <= upTo).ToList());
    }

    private static DateTime D(int month, int day) => new(2026, month, day, 0, 0, 0, DateTimeKind.Unspecified);

    private static StockValueDocument Doc(string type, int id, string number, string? reason = null)
        => new(type, id.ToString(), number, reason ?? number, 1, new DateTime(2026, 11, 20, 8, 0, 0, DateTimeKind.Utc));

    private static AgnumDocumentOutbox Row(string type, int id, string number, DateOnly date, decimal net, string status, string kind = "invoice", int revision = 1)
    {
        var payload = AgnumPayloadBuilder.Build(new PostingPayloadInput(
            Guid.NewGuid(), revision, number, date, 62, 493, 26, "EUR", 1m, net, 0m, "PVM1", "20110", [], Kind: kind));
        return new AgnumDocumentOutbox
        {
            Key = Guid.Parse(payload.Request.Key), Revision = revision, DocumentType = type, DocumentId = id, SupplierId = 1,
            DocumentNumber = number, DocumentDate = date, Currency = "EUR", GrossAmount = net, PayloadJson = payload.Json,
            PayloadHash = payload.Hash, Status = status, CreatedBy = "test"
        };
    }

    /// <summary>October 2026: receipt with invoice, issue, write-off, credit note, goods without invoice, invoice without goods.</summary>
    private static async Task<(WarehouseDbContext Db, FakeSource Source, MonthEndService Service, decimal LedgerValue)> ScenarioAsync(
        string name, bool unsentDocument = false)
    {
        var db = new WarehouseDbContext(new DbContextOptionsBuilder<WarehouseDbContext>().UseInMemoryDatabase(name).Options);
        db.StockAccountProfiles.Add(new StockAccountProfile { Id = 1, Name = "Materials", StockAccount = "20110", AgnumSndId = 1, IsDefault = true });
        var source = new FakeSource();
        var item = new ItemValuation();
        void Add(params StockValueChanged?[] events)
        {
            foreach (var e in events.Where(e => e is not null))
            {
                item.Apply(e!);
                source.Events.Add(e!);
            }
        }

        // shipment 1: 10 units for 100, invoice of 100 posted and sent
        Add(item.Inflow(1, StockValueKinds.Receipt, 10m, 100m, Doc("InboundShipment", 1, "SH-1"), D(10, 6)));
        db.AgnumOutbox.Add(Row("InboundShipment", 1, "INV-1", new DateOnly(2026, 10, 5), 100m, OutboxStatus.Sent));
        // issue 4, write-off 1
        Add(item.Outflow(1, StockValueKinds.Issue, 4m, Doc("SalesOrder", 7, "SO-7"), D(10, 12)));
        Add(item.Outflow(1, StockValueKinds.Scrap, 1m, Doc("StockMoved", 8, "SCRAP-1", "Damaged"), D(10, 20)));
        // credit note of 12 without goods: 6 on stock (revaluation) and 6 price difference
        Add(item.SupplierCredit(1, 0m, 12m, 10m, Doc("SupplierCreditDocument", 1, "CR-1"), D(10, 25)).ToArray());
        db.AgnumOutbox.Add(Row("SupplierCreditDocument", 1, "CR-1", new DateOnly(2026, 10, 25), 12m, OutboxStatus.Sent, "credit"));
        // shipment 2: goods received in October, invoice not posted
        Add(item.Inflow(1, StockValueKinds.Receipt, 5m, 50m, Doc("InboundShipment", 2, "SH-2"), D(10, 28)));
        // shipment 3: invoice of 30 dated and sent in October, goods arrive in November
        db.AgnumOutbox.Add(Row("InboundShipment", 3, "INV-3", new DateOnly(2026, 10, 30), 30m, OutboxStatus.Sent));
        var ledger = item.ValueEur;
        Add(item.Inflow(1, StockValueKinds.Receipt, 3m, 30m, Doc("InboundShipment", 3, "SH-3"), D(11, 3)));
        if (unsentDocument)
            db.AgnumOutbox.Add(Row("InboundShipment", 4, "INV-4", new DateOnly(2026, 10, 31), 10m, OutboxStatus.Pending));
        await db.SaveChangesAsync();
        return (db, source, new MonthEndService(db, source, new TestTime()), ledger);
    }

    [Fact]
    public async Task Statement_ReconcilesWithZeroResidual_AndBridgeExplainsEveryDifference()
    {
        var (db, _, service, ledger) = await ScenarioAsync(nameof(Statement_ReconcilesWithZeroResidual_AndBridgeExplainsEveryDifference));
        var report = await service.GetReportAsync(2026, 10, default);

        var row = report.Statement.Should().ContainSingle().Subject;
        row.Opening.Should().Be(0m);
        row.Receipts.Should().Be(150m);
        row.Issues.Should().Be(-40m);
        row.Scrap.Should().Be(-10m);
        row.Revaluation.Should().Be(-6m);
        row.PriceDifference.Should().Be(-6m);
        row.Closing.Should().Be(ledger);
        row.Residual.Should().Be(0m);

        var bridge = report.Bridge.Should().ContainSingle().Subject;
        bridge.AgnumDebitTurnover.Should().Be(118m);
        bridge.WarehouseTurnover.Should().Be(138m);
        bridge.Unexplained.Should().Be(0m);
        bridge.AgnumMore.Should().ContainSingle(d => d.Number == "INV-3" && d.Difference == 30m && d.Reason == "InvoicedNotReceived");
        bridge.WarehouseMore.Should().ContainSingle(d => d.Number == "SH-2" && d.Difference == -50m && d.Reason == "NotInvoiced");

        report.ActGroups.Should().Contain(g => g.Act == "WriteOff" && g.Reason == "Damaged");
        db.Dispose();
    }

    [Fact]
    public async Task Close_IsBlockedByAnUnsentDocument()
    {
        var (db, _, service, _) = await ScenarioAsync(nameof(Close_IsBlockedByAnUnsentDocument), unsentDocument: true);

        var act = () => service.CloseAsync(2026, 10, "accountant", default);

        var ex = (await act.Should().ThrowAsync<MonthEndException>()).Which;
        ex.Blockers.Should().Contain(b => b.Contains("INV-4"));
        (await service.IsClosedAsync(2026, 10, default)).Should().BeFalse();
        db.Dispose();
    }

    [Fact]
    public async Task Close_FreezesTheMonth_RebuildDoesNotChangeIt_AndReopenIsAudited()
    {
        var (db, source, service, _) = await ScenarioAsync(nameof(Close_FreezesTheMonth_RebuildDoesNotChangeIt_AndReopenIsAudited));
        var before = await service.GetReportAsync(2026, 10, default);

        await service.CloseAsync(2026, 10, "accountant", default);

        (await AccountingPeriods.CheckAsync(db, default, new DateOnly(2026, 10, 15)))!.Code.Should().Be("PeriodClosed");
        (await AccountingPeriods.CheckAsync(db, default, new DateOnly(2026, 11, 15))).Should().BeNull();

        // a rebuilt ledger that differs (as a rebuild after a defect would) must not change the closed month
        source.Events.RemoveAll(e => e.Kind == StockValueKinds.Scrap);
        var after = await service.GetReportAsync(2026, 10, default);
        after.Statement.Single().Closing.Should().Be(before.Statement.Single().Closing);
        after.Statement.Single().Scrap.Should().Be(before.Statement.Single().Scrap);

        var reopen = () => service.ReopenAsync(2026, 10, " ", "accountant", default);
        await reopen.Should().ThrowAsync<MonthEndException>();
        await service.ReopenAsync(2026, 10, "Correct the scrap act", "accountant", default);
        (await AccountingPeriods.CheckAsync(db, default, new DateOnly(2026, 10, 15))).Should().BeNull();
        db.AccountingConfigAudit.Where(a => a.Entity == "AccountingPeriod").Select(a => a.Action).ToList()
            .Should().Equal("Close", "Reopen");
        (await service.GetReportAsync(2026, 10, default)).Statement.Single().Scrap.Should().Be(0m);
        db.Dispose();
    }

    [Fact]
    public async Task Close_IsRefusedForAMonthThatHasNotEnded()
    {
        var (db, _, service, _) = await ScenarioAsync(nameof(Close_IsRefusedForAMonthThatHasNotEnded));
        var act = () => service.CloseAsync(2026, 11, "accountant", default);
        await act.Should().ThrowAsync<MonthEndException>();
        db.Dispose();
    }
}

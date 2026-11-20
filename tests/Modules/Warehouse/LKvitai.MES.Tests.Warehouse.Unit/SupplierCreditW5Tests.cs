using FluentAssertions;
using LKvitai.MES.Contracts.Events;
using LKvitai.MES.Modules.Warehouse.Api.Services;
using LKvitai.MES.Modules.Warehouse.Domain.Aggregates;
using LKvitai.MES.Modules.Warehouse.Domain.Entities;
using LKvitai.MES.Modules.Warehouse.Infrastructure.Fx;
using LKvitai.MES.Modules.Warehouse.Infrastructure.Purchasing;
using Xunit;

namespace LKvitai.MES.Tests.Warehouse.Unit;

/// <summary>Blueprint stage W5: credit notes and supplier returns.</summary>
public sealed class SupplierCreditW5Tests
{
    private static readonly PostingRate EurRate = new(FxRateStatus.Ok, 1m, new DateOnly(2026, 11, 20), "EUR", null);

    private static ItemValuation Stock(decimal qty, decimal value)
    {
        var v = new ItemValuation();
        v.Apply(v.Inflow(10, StockValueKinds.Receipt, qty, value, new StockValueDocument("InboundShipment", "1", "IN-1", "test")));
        return v;
    }

    private static SupplierCreditDocument Credit(string kind, decimal net, decimal returnQty, decimal receivedQty)
    {
        var item = new Item { Id = 10, InternalSKU = "SKU", ItemType = ItemType.Stock };
        return new SupplierCreditDocument
        {
            Id = 1,
            DocumentKind = kind,
            CreditNumber = "CN-1",
            OriginalInvoiceNumber = "INV-1",
            NetAmount = net,
            Lines =
            [
                new SupplierCreditLine
                {
                    Id = 1, ItemId = 10, Item = item, ReturnQty = returnQty, NetAmount = net,
                    OriginalLine = new InboundShipmentLine { Id = 5, ItemId = 10, ReceivedQty = receivedQty }
                }
            ]
        };
    }

    private static IReadOnlyList<StockValueChanged> Run(ItemValuation v, SupplierCreditDocument credit)
    {
        var requests = PurchaseDocumentService.ApplyCredit(credit, EurRate, "tester", 1);
        var events = new List<StockValueChanged>();
        foreach (var request in requests)
        {
            foreach (var e in StockValueWriter.BuildMany(v, 10, request))
            {
                v.Apply(e);
                events.Add(e);
            }
        }

        return events;
    }

    [Fact]
    public void Return10Of11At10_Credit120_SplitsIntoReturnStockAndPriceDifference_AgnumGets120Negative()
    {
        var v = Stock(11m, 110m);
        var credit = Credit(SupplierCreditDocument.KindSupplierReturn, 120m, 10m, 11m);

        var events = Run(v, credit);

        events.Should().HaveCount(3);
        events[0].Kind.Should().Be(StockValueKinds.SupplierReturn);
        events[0].ValueDeltaEur.Should().Be(-100m);
        events[0].QtyDelta.Should().Be(-10m);
        events[1].Kind.Should().Be(StockValueKinds.Revaluation);
        events[1].ValueDeltaEur.Should().Be(-10m);
        events[2].Kind.Should().Be(StockValueKinds.PriceDifference);
        events[2].OffLedgerEur.Should().Be(-10m);
        events[2].ValueDeltaEur.Should().Be(0m);
        v.ValueEur.Should().Be(0.00m);
        v.ValuedQty.Should().Be(1m);
        credit.AgnumNetEur.Should().Be(-120m);
        credit.NetEur.Should().Be(120m, "amounts are entered positive");
        credit.PostingStatus.Should().Be("Posted");
    }

    [Fact]
    public void Discount500_WhenStockValueIs300_StockStaysNonNegative_ExcessIsPriceDifference()
    {
        var v = Stock(10m, 300m);
        var credit = Credit(SupplierCreditDocument.KindCreditNote, 500m, 0m, 10m);

        var events = Run(v, credit);

        events.Should().HaveCount(2);
        events[0].Kind.Should().Be(StockValueKinds.Revaluation);
        events[0].ValueDeltaEur.Should().Be(-300m);
        events[1].Kind.Should().Be(StockValueKinds.PriceDifference);
        events[1].OffLedgerEur.Should().Be(-200m);
        v.ValueEur.Should().Be(0m);
        v.ValuedQty.Should().Be(10m);
        credit.AgnumNetEur.Should().Be(-500m);
    }

    [Fact]
    public void Discount_SplitsByShareOfReceivedQuantityStillOnStock()
    {
        // 10 received, 4 still on stock worth 40 -> 40% of the discount 50 = 20 lowers the stock, 30 is a price difference.
        var v = Stock(4m, 40m);
        var events = Run(v, Credit(SupplierCreditDocument.KindCreditNote, 50m, 0m, 10m));

        events[0].ValueDeltaEur.Should().Be(-20m);
        events[1].OffLedgerEur.Should().Be(-30m);
        v.ValueEur.Should().Be(20m);
    }

    [Fact]
    public void ReturnWithCreditAboveAverage_TakesTheDifferenceFromRemainingStock()
    {
        var v = Stock(10m, 100m);
        var events = Run(v, Credit(SupplierCreditDocument.KindSupplierReturn, 80m, 5m, 10m));

        events.Should().HaveCount(2);
        events[0].ValueDeltaEur.Should().Be(-50m);
        events[1].Kind.Should().Be(StockValueKinds.Revaluation);
        events[1].ValueDeltaEur.Should().Be(-30m);
        v.ValueEur.Should().Be(20m);
    }

    [Fact]
    public void ReturnWithCreditBelowAverage_DifferenceIsPositivePriceDifference()
    {
        var v = Stock(10m, 100m);
        var events = Run(v, Credit(SupplierCreditDocument.KindSupplierReturn, 30m, 5m, 10m));

        events.Should().HaveCount(2);
        events[1].Kind.Should().Be(StockValueKinds.PriceDifference);
        events[1].OffLedgerEur.Should().Be(20m);
        v.ValueEur.Should().Be(50m);
    }

[Fact]
    public void CreditInForeignCurrency_ConvertsAndAllocatesLinesExactly()
    {
        var credit = Credit(SupplierCreditDocument.KindCreditNote, 100m, 0m, 10m);
        credit.VatAmount = 21m;
        PurchaseDocumentService.ApplyCredit(credit, new PostingRate(FxRateStatus.Ok, 2m, new DateOnly(2026, 11, 20), "BankOfLithuania", null), "t", null);

        credit.NetEur.Should().Be(50m);
        credit.VatEur.Should().Be(10.5m);
        credit.AgnumGrossEur.Should().Be(-60.5m);
        credit.Lines.Sum(l => l.NetEur!.Value).Should().Be(credit.NetEur);
    }
}

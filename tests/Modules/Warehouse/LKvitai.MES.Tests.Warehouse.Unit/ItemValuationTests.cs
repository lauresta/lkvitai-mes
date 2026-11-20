using FluentAssertions;
using LKvitai.MES.Contracts.Events;
using LKvitai.MES.Modules.Warehouse.Api.Services;
using LKvitai.MES.Modules.Warehouse.Domain.Aggregates;
using LKvitai.MES.BuildingBlocks.SharedKernel;
using Xunit;

namespace LKvitai.MES.Tests.Warehouse.Unit;

[Trait("Category", "Valuation")]
[Trait("Category", "Domain")]
public class ItemValuationTests
{
    private static readonly StockValueDocument Doc = new("Test", "1", "T-1", "reason");

    private static ItemValuation WithStock(decimal qty, decimal value)
    {
        var v = new ItemValuation();
        v.Apply(v.Inflow(10, StockValueKinds.Receipt, qty, value, Doc));
        return v;
    }

    [Fact]
    public void StreamIdFor_ShouldUseSprint7Format() => ItemValuation.StreamIdFor(42).Should().Be("valuation-item-42");

    [Fact]
    public void StreamIdFor_WithInvalidItemId_ShouldThrow()
    {
        var action = () => ItemValuation.StreamIdFor(0);
        action.Should().Throw<DomainException>().Which.ErrorCode.Should().Be(DomainErrorCodes.ValidationError);
    }

    [Fact]
    public void Receipt_ShouldIncreaseValuedQtyAndValue_AndDeriveUnitCost()
    {
        var v = WithStock(10m, 35.55m);
        v.ValuedQty.Should().Be(10m);
        v.ValueEur.Should().Be(35.55m);
        v.UnitCost.Should().Be(3.555m);
    }

    [Fact]
    public void Issue_ShouldTakeAverageValue_AndRoundToCents()
    {
        var v = WithStock(3m, 10m);
        var issue = v.Outflow(10, StockValueKinds.Issue, 1m, Doc)!;
        issue.QtyDelta.Should().Be(-1m);
        issue.ValueDeltaEur.Should().Be(-3.33m);
    }

    [Fact]
    public void Issue_OfLastUnits_ShouldTakeWholeRemainingValue()
    {
        var v = WithStock(3m, 10m);
        v.Apply(v.Outflow(10, StockValueKinds.Issue, 1m, Doc)!);
        v.Apply(v.Outflow(10, StockValueKinds.Issue, 1m, Doc)!);
        var last = v.Outflow(10, StockValueKinds.Issue, 1m, Doc)!;
        v.Apply(last);
        v.ValuedQty.Should().Be(0m);
        v.ValueEur.Should().Be(0m);
        v.UnitCost.Should().Be(3.33m, "the last known unit cost is kept");
    }

    [Fact]
    public void Issue_BeyondValuedQty_ShouldTakeOnlyValuedQuantity()
    {
        var v = WithStock(2m, 8m);
        var issue = v.Outflow(10, StockValueKinds.Issue, 5m, Doc)!;
        issue.QtyDelta.Should().Be(-2m);
        issue.ValueDeltaEur.Should().Be(-8m);
    }

    [Fact]
    public void Issue_WhenNothingIsValued_ShouldProduceNoEvent()
        => new ItemValuation().Outflow(10, StockValueKinds.Issue, 1m, Doc).Should().BeNull();

    [Fact]
    public void ReturnIn_ShouldComeInAtCurrentAverage()
    {
        var v = WithStock(4m, 10m);
        var back = v.InflowAtAverage(10, StockValueKinds.ReturnIn, 2m, Doc);
        back.ValueDeltaEur.Should().Be(5m);
    }

    [Fact]
    public void ReceiptOntoExistingStock_ShouldIncreaseValueByExactlyQtyTimesPrice()
    {
        var v = WithStock(1237.5m, ItemValuation.Money(1237.5m * 3.4567m));
        var before = v.ValueEur;
        var receipt = v.Inflow(10, StockValueKinds.Receipt, 480m, ItemValuation.Money(480m * 3.61m), Doc);
        v.Apply(receipt);
        (v.ValueEur - before).Should().Be(1732.80m);
    }

    [Fact]
    public void Revalue_ShouldLowerValueToNewUnitPrice()
    {
        var v = WithStock(10m, 100m);
        var evt = v.Revalue(10, 4m, Doc);
        evt.Kind.Should().Be(StockValueKinds.Revaluation);
        evt.QtyDelta.Should().Be(0m);
        evt.ValueDeltaEur.Should().Be(-60m);
    }

    [Fact]
    public void Revalue_UpwardOrWithoutStock_ShouldThrow()
    {
        WithStock(10m, 100m).Invoking(x => x.Revalue(10, 20m, Doc)).Should().Throw<DomainException>();
        new ItemValuation().Invoking(x => x.Revalue(10, 1m, Doc)).Should().Throw<DomainException>();
    }

    [Fact]
    public void Change_ThatMakesValueOrQuantityNegative_ShouldThrow()
    {
        var v = WithStock(2m, 8m);
        v.Invoking(x => x.Change(10, StockValueKinds.AdjustmentOut, -3m, -1m, Doc)).Should().Throw<DomainException>();
        v.Invoking(x => x.Change(10, StockValueKinds.PriceDifference, 0m, -9m, Doc)).Should().Throw<DomainException>();
        v.Invoking(x => x.Change(10, "Nonsense", 0m, 0m, Doc)).Should().Throw<DomainException>();
    }

    [Fact]
    public void LegacyEvents_ShouldStillBeReadable()
    {
        var v = new ItemValuation();
        v.Apply(new ValuationInitialized { InventoryItemId = 10, InitialUnitCost = 2.5m });
        v.Apply(new CostAdjusted { InventoryItemId = 10, NewUnitCost = 3m });
        v.Apply(new LandedCostApplied { ItemId = 10, TotalLandedCost = 0.5m });
        v.Apply(new WrittenDown { ItemId = 10, NewValue = 1m });
        v.ValueEur.Should().Be(0m);
        v.UnitCost.Should().Be(1m);
    }

    [Theory]
    [InlineData("A-01-01", true)]
    [InlineData("RECEIVING", true)]
    [InlineData("QC_HOLD", true)]
    [InlineData("QUARANTINE", true)]
    [InlineData("SHIPPING", true)]
    [InlineData("IN_TRANSIT_TR-1", true)]
    [InlineData("WAREHOUSE_MERGE", true)]
    [InlineData("SUPPLIER", false)]
    [InlineData("PRODUCTION", false)]
    [InlineData("SCRAP", false)]
    [InlineData("CUSTOMER", false)]
    [InlineData("RETURN_TO_SUPPLIER", false)]
    [InlineData("ADJUSTMENT", false)]
    [InlineData("SYSTEM", false)]
    [InlineData("AGNUM", false)]
    public void StockBoundary_ShouldClassifyLocationCodes(string code, bool inside)
        => StockBoundary.IsInside(code).Should().Be(inside);

    [Theory]
    [InlineData("SHIPPING", "CUSTOMER", StockValueKinds.Issue, false)]
    [InlineData("A-01", "PRODUCTION", StockValueKinds.Issue, false)]
    [InlineData("QUARANTINE", "SCRAP", StockValueKinds.Scrap, false)]
    [InlineData("QUARANTINE", "RETURN_TO_SUPPLIER", StockValueKinds.SupplierReturn, false)]
    [InlineData("A-01", "ADJUSTMENT", StockValueKinds.AdjustmentOut, false)]
    [InlineData("CUSTOMER", "RECEIVING", StockValueKinds.ReturnIn, true)]
    [InlineData("PRODUCTION", "RECEIVING", StockValueKinds.ReturnIn, true)]
    [InlineData("ADJUSTMENT", "A-01", StockValueKinds.AdjustmentIn, true)]
    public void Movement_AcrossTheBoundary_ShouldProduceValueRequest(string from, string to, string kind, bool inflow)
    {
        var request = StockValueRequest.ForMovement("SKU", 2m, from, to, Doc);
        request.Should().NotBeNull();
        request!.Kind.Should().Be(kind);
        request.IsInflow.Should().Be(inflow);
    }

    [Theory]
    [InlineData("RECEIVING", "A-01")]
    [InlineData("A-01", "B-02")]
    [InlineData("SHIPPING", "RECEIVING")]
    [InlineData("A-01", "IN_TRANSIT_TR-1")]
    [InlineData("WAREHOUSE_MERGE", "A-01")]
    [InlineData("CUSTOMER", "SCRAP")]
    public void Movement_InsideTheBoundary_ShouldNotChangeValue(string from, string to)
        => StockValueRequest.ForMovement("SKU", 2m, from, to, Doc).Should().BeNull();

    [Fact]
    public void Adjustment_ShouldMapSignToKind_AndIgnoreOutsideLocations()
    {
        StockValueRequest.ForAdjustment("SKU", 3m, "A-01", Doc)!.Kind.Should().Be(StockValueKinds.AdjustmentIn);
        StockValueRequest.ForAdjustment("SKU", -3m, "A-01", Doc)!.Kind.Should().Be(StockValueKinds.AdjustmentOut);
        StockValueRequest.ForAdjustment("SKU", -3m, "AGNUM", Doc).Should().BeNull();
        StockValueRequest.ForAdjustment("SKU", 0m, "A-01", Doc).Should().BeNull();
    }

    [Fact]
    public void BusinessDate_ShouldUseVilniusCalendarDate()
    {
        // 2026-12-31 22:30 UTC is already 2027-01-01 in Vilnius (UTC+2).
        StockBusinessDate.From(new DateTime(2026, 12, 31, 22, 30, 0, DateTimeKind.Utc))
            .Should().Be(new DateTime(2027, 1, 1));
    }

    [Fact]
    public void RevaluationPolicy_ShouldRequireManagerApprovalAbove1000()
    {
        ValuationRevaluationPolicy.ValidateApproval(1000m, null, false).IsSuccess.Should().BeTrue();
        ValuationRevaluationPolicy.ValidateApproval(1000.01m, null, true).IsSuccess.Should().BeFalse();
        ValuationRevaluationPolicy.ValidateApproval(1000.01m, "boss", false).IsSuccess.Should().BeFalse();
        ValuationRevaluationPolicy.ValidateApproval(1000.01m, "boss", true).IsSuccess.Should().BeTrue();
    }
}

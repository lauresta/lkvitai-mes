using LKvitai.MES.Contracts.Events;
using LKvitai.MES.Modules.Warehouse.Api.Services;
using LKvitai.MES.Modules.Warehouse.Domain;
using LKvitai.MES.Modules.Warehouse.Domain.Aggregates;
using Xunit;

namespace LKvitai.MES.Tests.Warehouse.Unit;

public sealed class SupplierStockReportTests
{
    [Theory]
    [InlineData(100, 30, 70)]
    [InlineData(100, 100, 0)]
    [InlineData(100, 120, 0)]
    public void Open_quantity_is_what_is_still_expected_and_never_negative(decimal expected, decimal received, decimal open)
        => Assert.Equal(open, SupplierStockMath.OpenQty(expected, received));

    [Fact]
    public void Shortage_counts_stock_on_hand_and_what_is_already_ordered()
    {
        Assert.Equal(40m, SupplierStockMath.Shortage(100m, 50m, 10m));
        Assert.Equal(0m, SupplierStockMath.Shortage(100m, 50m, 80m));
        Assert.Equal(0m, SupplierStockMath.Shortage(0m, 0m, 0m));
        Assert.Null(SupplierStockMath.Shortage(null, 0m, 0m));
    }

    [Fact]
    public void Average_per_month_divides_by_the_period()
    {
        Assert.Equal(33.333m, SupplierStockMath.AveragePerMonth(100m, 3));
        Assert.Equal(0m, SupplierStockMath.AveragePerMonth(100m, 0));
    }

    [Theory]
    [InlineData("PRODUCTION", SupplierStockIssueKind.Production)]
    [InlineData(" production ", SupplierStockIssueKind.Production)]
    [InlineData("CUSTOMER", SupplierStockIssueKind.Dispatch)]
    [InlineData("SCRAP", SupplierStockIssueKind.WriteOff)]
    public void A_movement_out_of_the_warehouse_counts_by_its_destination(string destination, SupplierStockIssueKind kind)
        => Assert.Equal(kind, SupplierStockMath.KindOf(destination));

    [Theory]
    [InlineData("A1-01-02")]
    [InlineData("RECEIVING")]
    [InlineData(null)]
    public void A_move_inside_the_warehouse_is_not_an_issue(string? destination)
        => Assert.Null(SupplierStockMath.KindOf(destination));

    [Fact]
    public void An_issue_to_production_crosses_the_stock_boundary_and_is_valued_as_an_issue()
    {
        var request = StockValueRequest.ForMovement("RM-1", 5m, "A1-01-02", "PRODUCTION", new StockValueDocument("ProductionIssue", "m1", "GU-1", null));
        Assert.NotNull(request);
        Assert.Equal(StockValueKinds.Issue, request!.Kind);
        Assert.False(request.IsInflow);
        Assert.True(MovementType.IsBalanceDecreasing(MovementType.ProductionIssue));
    }

    [Fact]
    public void Reference_issuer_and_note_survive_the_single_reason_text()
    {
        var reason = ProductionIssueReason.Compose("GU-1234", "vstropus", "Skubu\nantra eilutė");
        Assert.Equal(("GU-1234", "vstropus", "Skubu\nantra eilutė"), ProductionIssueReason.Parse(reason));
        Assert.Equal((null, "vstropus", null), ProductionIssueReason.Parse(ProductionIssueReason.Compose(null, "vstropus", " ")));
        // A prefix further down is part of the note, not a reference.
        const string note = "just a note\nRef: typed by hand later";
        Assert.Equal((null, null, note), ProductionIssueReason.Parse(note));
        Assert.Null(ProductionIssueReason.Compose(null, null, null));
        Assert.Equal((null, null, null), ProductionIssueReason.Parse(null));
    }
}

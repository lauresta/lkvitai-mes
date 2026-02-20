using FluentAssertions;
using LKvitai.MES.Modules.Warehouse.Domain;
using Xunit;

namespace LKvitai.MES.Tests.Warehouse.Unit;

/// <summary>
/// Tests for StockLedgerStreamId helper — ensures (warehouseId, location, sku)
/// partition key enforcement per ADR-001.
/// </summary>
public class StockLedgerStreamIdTests
{
    // ── For: valid inputs ─────────────────────────────────────────────────

    [Fact]
    public void For_ShouldReturn_ThreePartStreamId()
    {
        StockLedgerStreamId.For("WH1", "LOC-A", "SKU-001")
            .Should().Be("stock-ledger:WH1:LOC-A:SKU-001");
    }

    [Fact]
    public void For_ShouldHandleHyphensInParts()
    {
        StockLedgerStreamId.For("warehouse-42", "R3-C6", "SKU-933")
            .Should().Be("stock-ledger:warehouse-42:R3-C6:SKU-933");
    }

    // ── For: invalid inputs ───────────────────────────────────────────────

    [Theory]
    [InlineData(null, "LOC", "SKU")]
    [InlineData("", "LOC", "SKU")]
    [InlineData("   ", "LOC", "SKU")]
    public void For_ShouldThrow_WhenWarehouseIdInvalid(string? warehouseId, string location, string sku)
    {
        var act = () => StockLedgerStreamId.For(warehouseId!, location, sku);
        act.Should().Throw<ArgumentException>().And.ParamName.Should().Be("warehouseId");
    }

    [Theory]
    [InlineData("WH1", null, "SKU")]
    [InlineData("WH1", "", "SKU")]
    [InlineData("WH1", "   ", "SKU")]
    public void For_ShouldThrow_WhenLocationInvalid(string warehouseId, string? location, string sku)
    {
        var act = () => StockLedgerStreamId.For(warehouseId, location!, sku);
        act.Should().Throw<ArgumentException>().And.ParamName.Should().Be("location");
    }

    [Theory]
    [InlineData("WH1", "LOC", null)]
    [InlineData("WH1", "LOC", "")]
    [InlineData("WH1", "LOC", "   ")]
    public void For_ShouldThrow_WhenSkuInvalid(string warehouseId, string location, string? sku)
    {
        var act = () => StockLedgerStreamId.For(warehouseId, location, sku!);
        act.Should().Throw<ArgumentException>().And.ParamName.Should().Be("sku");
    }

    // ── Parse: valid round-trip ───────────────────────────────────────────

    [Fact]
    public void Parse_ShouldReturn_CorrectParts()
    {
        var (wh, loc, sku) = StockLedgerStreamId.Parse("stock-ledger:WH1:LOC-A:SKU-001");
        wh.Should().Be("WH1");
        loc.Should().Be("LOC-A");
        sku.Should().Be("SKU-001");
    }

    [Fact]
    public void For_ThenParse_ShouldRoundTrip()
    {
        var streamId = StockLedgerStreamId.For("main", "R3-C6", "SKU-933");
        var (wh, loc, sku) = StockLedgerStreamId.Parse(streamId);
        wh.Should().Be("main");
        loc.Should().Be("R3-C6");
        sku.Should().Be("SKU-933");
    }

    // ── Parse: invalid inputs ─────────────────────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("invalid")]
    [InlineData("stock-ledger:WH1")]
    [InlineData("stock-ledger:WH1:LOC")]
    [InlineData("wrong-prefix:WH1:LOC:SKU")]
    public void Parse_ShouldThrow_WhenFormatInvalid(string? streamId)
    {
        var act = () => StockLedgerStreamId.Parse(streamId!);
        act.Should().Throw<ArgumentException>();
    }
}

using System.Reflection;
using FluentAssertions;
using LKvitai.MES.Modules.Warehouse.Api.Controllers;
using Xunit;

namespace LKvitai.MES.Tests.Warehouse.Unit;

public sealed class LocationBalanceCsvPriceTests
{
    [Fact]
    public void MissingPricesExportAsEmptyCsvCells()
    {
        var row = new StockController.LocationBalanceItemDto(
            7, "R3-C6-L1-B1", 2, 0m, 0m, null, null, null, null, "Active", null)
        {
            TotalCostValue = null,
            TotalSaleValue = null
        };
        var method = typeof(StockController).GetMethod("BuildLocationBalanceCsv",
            BindingFlags.NonPublic | BindingFlags.Static)!;

        var csv = (string)method.Invoke(null, [new[] { row }])!;

        csv.Split('\n', StringSplitOptions.RemoveEmptyEntries).Last()
            .Should().Be("7,R3-C6-L1-B1,2,0,0,,,,,Active,,,");
    }
}

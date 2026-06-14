using FluentAssertions;
using LKvitai.MES.Modules.Warehouse.Api.Controllers;
using LKvitai.MES.Modules.Warehouse.Domain.Entities;
using Xunit;

namespace LKvitai.MES.Tests.Warehouse.Unit;

public class AvailableStockMetadataFilterTests
{
    [Fact]
    public void FilterItemIds_WhenSupplierCountryTagAndNameProvided_ReturnsIntersection()
    {
        // Arrange
        var target = new Item
        {
            Id = 10,
            InternalSKU = "WOOD-001",
            Name = "Oak board",
            ProductConfigId = "PRD-OAK"
        };
        var wrongCountry = new Item
        {
            Id = 20,
            InternalSKU = "WOOD-002",
            Name = "Oak board premium"
        };
        var wrongTag = new Item
        {
            Id = 30,
            InternalSKU = "WOOD-003",
            Name = "Oak board reserve"
        };

        var supplierLt = new Supplier { Id = 1, Code = "SUP-LT", Name = "Baltic Wood", Country = "Lithuania" };
        var supplierPl = new Supplier { Id = 2, Code = "SUP-PL", Name = "Baltic Wood PL", Country = "Poland" };

        var mappings = new[]
        {
            new SupplierItemMapping { ItemId = target.Id, SupplierId = supplierLt.Id, SupplierSKU = "BW-001", Supplier = supplierLt },
            new SupplierItemMapping { ItemId = wrongCountry.Id, SupplierId = supplierPl.Id, SupplierSKU = "BW-002", Supplier = supplierPl },
            new SupplierItemMapping { ItemId = wrongTag.Id, SupplierId = supplierLt.Id, SupplierSKU = "BW-003", Supplier = supplierLt }
        };

        var photos = new[]
        {
            new ItemPhoto { ItemId = target.Id, Tags = "#oak, surfaced" },
            new ItemPhoto { ItemId = wrongCountry.Id, Tags = "#oak" },
            new ItemPhoto { ItemId = wrongTag.Id, Tags = "#pine" }
        };

        var filters = new AvailableStockMetadataFilters(
            Item: "WOOD*",
            ItemName: "Oak",
            Tag: "#oak",
            Supplier: "Baltic",
            SupplierCountry: "Lithuania");

        // Act
        var result = StockMetadataFilter.FilterItemIds(
            [target, wrongCountry, wrongTag],
            mappings,
            photos,
            filters);

        // Assert
        result.Should().Equal(target.Id);
    }
}

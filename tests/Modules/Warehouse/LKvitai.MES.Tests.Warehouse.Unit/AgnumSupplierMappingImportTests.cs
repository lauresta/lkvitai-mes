using FluentAssertions;
using LKvitai.MES.Modules.Warehouse.Domain.Entities;
using LKvitai.MES.Modules.Warehouse.Infrastructure.Agnum;
using LKvitai.MES.Modules.Warehouse.Infrastructure.Persistence;
using LKvitai.MES.Modules.Warehouse.Integration.Agnum;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace LKvitai.MES.Tests.Warehouse.Unit;

public class AgnumSupplierMappingImportTests
{
    [Fact]
    public async Task ApplyAsync_WhenProductHasNoSupplierCode_ShouldNotStoreItemCodeAsSupplierCode()
    {
        await using var db = CreateDbContext();
        db.UnitOfMeasures.Add(new UnitOfMeasure { Code = "vnt", Name = "vnt", Type = "Piece" });
        await db.SaveChangesAsync();

        var product = new AgnumProductDto
        {
            Id = 1,
            Code = "OWN-CODE-1",
            Name = "Item without supplier code",
            Pcs = "vnt",
            Enabled = true,
            SupplierCode = "SUP1",
            SupplierName = "Supplier One"
        };

        var result = await CreateService(db, product).ApplyAsync(493);

        result.Created.Should().Be(1);
        (await db.Items.CountAsync()).Should().Be(1);
        (await db.Suppliers.CountAsync()).Should().Be(1);
        (await db.SupplierItemMappings.CountAsync()).Should().Be(0);
    }

    [Theory]
    [InlineData("SUP-SKU-9", null, "SUP-SKU-9")]
    [InlineData(null, " F2-CODE ", "F2-CODE")]
    public async Task ApplyAsync_WhenProductHasSupplierCode_ShouldCreateMappingWithIt(
        string? supplierSku, string? f2, string expected)
    {
        await using var db = CreateDbContext();
        db.UnitOfMeasures.Add(new UnitOfMeasure { Code = "vnt", Name = "vnt", Type = "Piece" });
        await db.SaveChangesAsync();

        var product = new AgnumProductDto
        {
            Id = 1,
            Code = "OWN-CODE-1",
            Name = "Item with supplier code",
            Pcs = "vnt",
            Enabled = true,
            SupplierCode = "SUP1",
            SupplierName = "Supplier One",
            SupplierSku = supplierSku,
            F2 = f2
        };

        await CreateService(db, product).ApplyAsync(493);

        var mapping = await db.SupplierItemMappings.SingleAsync();
        mapping.SupplierSKU.Should().Be(expected);
    }

    private static AgnumNomenclatureImportService CreateService(WarehouseDbContext db, params AgnumProductDto[] products)
    {
        var clientMock = new Mock<IAgnumApiClient>();
        clientMock.Setup(x => x.GetProductsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(products.ToList());
        clientMock.Setup(x => x.GetClientsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<AgnumClientDto>());

        var factoryMock = new Mock<IAgnumApiClientFactory>();
        factoryMock.Setup(x => x.GetForSndId(It.IsAny<int>())).Returns(clientMock.Object);

        return new AgnumNomenclatureImportService(db, factoryMock.Object, new LoggerFactory().CreateLogger<AgnumNomenclatureImportService>());
    }

    private static WarehouseDbContext CreateDbContext()
        => new(new DbContextOptionsBuilder<WarehouseDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
}

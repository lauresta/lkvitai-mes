using FluentAssertions;
using LKvitai.MES.Modules.Warehouse.Application.Ports;
using LKvitai.MES.Modules.Warehouse.Domain.Entities;
using LKvitai.MES.Modules.Warehouse.Infrastructure.Agnum;
using LKvitai.MES.Modules.Warehouse.Infrastructure.Persistence;
using LKvitai.MES.Modules.Warehouse.Integration.Agnum;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace LKvitai.MES.Tests.Warehouse.Unit;

public class AgnumImportConflictDetectionTests
{
    [Fact]
    public async Task PreviewAsync_WhenPcsIsUnknown_ShouldReturnToCreate()
    {
        await using var db = CreateDbContext();
        await SeedUnitOfMeasuresAsync(db, "vnt");

        var product = new AgnumProductDto
        {
            Id = 1,
            Code = "SKU-UNKNOWN",
            Name = "Unknown UoM",
            Pcs = "unknown",
            Enabled = true,
            Balance = 1
        };

        var service = CreateService(db, product);

        var preview = await service.PreviewAsync(493);

        preview.Conflicts.Should().BeEmpty();
        preview.ToCreate.Should().ContainSingle(x => x.AgnumProductId == 1 && x.Pcs == "unknown");
    }

    [Fact]
    public async Task PreviewAsync_WhenItemSkuExistsWithoutCurrentWarehouseLink_ShouldLinkExistingItem()
    {
        await using var db = CreateDbContext();
        await SeedUnitOfMeasuresAsync(db, "vnt");

        db.Items.Add(new Item { InternalSKU = "SKU-001", Name = "Existing Item", BaseUoM = "vnt", CategoryId = await GetOrCreateCategoryIdAsync(db) });
        await db.SaveChangesAsync();

        var product = new AgnumProductDto
        {
            Id = 2,
            Code = "SKU-001",
            Name = "Duplicate SKU",
            Pcs = "vnt",
            Enabled = true,
            Balance = 1
        };

        var service = CreateService(db, product);

        var preview = await service.PreviewAsync(493);

        preview.Conflicts.Should().BeEmpty();
        preview.ToCreate.Should().ContainSingle(x =>
            x.AgnumProductId == 2
            && x.Code == "SKU-001"
            && x.ExistingItemId.HasValue);
    }

    [Fact]
    public async Task ApplyAsync_WhenItemSkuExistsWithoutCurrentWarehouseLink_ShouldCreateLinkToExistingItem()
    {
        await using var db = CreateDbContext();
        await SeedUnitOfMeasuresAsync(db, "vnt");

        db.Items.Add(new Item { InternalSKU = "SKU-001", Name = "Existing Item", BaseUoM = "vnt", CategoryId = await GetOrCreateCategoryIdAsync(db) });
        await db.SaveChangesAsync();

        var existingItem = await db.Items.SingleAsync(x => x.InternalSKU == "SKU-001");
        var product = new AgnumProductDto
        {
            Id = 2,
            Code = "SKU-001",
            Name = "Existing Item",
            Pcs = "vnt",
            Enabled = true,
            Balance = 1
        };

        var service = CreateService(db, product);

        var result = await service.ApplyAsync(496);

        result.Conflicts.Should().BeEmpty();
        result.Created.Should().Be(1);
        db.Items.Count(x => x.InternalSKU == "SKU-001").Should().Be(1);
        var link = await db.AgnumProductLinks.SingleAsync(x => x.SndId == 496 && x.AgnumProductId == 2);
        link.ItemId.Should().Be(existingItem.Id);
        link.AgnumCode.Should().Be("SKU-001");
    }

    [Fact]
    public async Task PreviewAsync_WhenLinkPointsToDifferentItem_ShouldReturnLinkedToDifferentItem()
    {
        await using var db = CreateDbContext();
        await SeedUnitOfMeasuresAsync(db, "vnt");

        var linkedItem = new Item { InternalSKU = "ITEM-ONE", Name = "Linked Item", BaseUoM = "vnt", CategoryId = await GetOrCreateCategoryIdAsync(db) };
        var conflictingItem = new Item { InternalSKU = "SKU-002", Name = "Conflicting Item", BaseUoM = "vnt", CategoryId = await GetOrCreateCategoryIdAsync(db) };
        db.Items.AddRange(linkedItem, conflictingItem);
        await db.SaveChangesAsync();

        db.AgnumProductLinks.Add(new AgnumProductLink
        {
            ItemId = linkedItem.Id,
            SndId = 493,
            AgnumProductId = 3,
            AgnumCode = "SKU-001",
            AgnumEnabled = true,
            LastImportedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var product = new AgnumProductDto
        {
            Id = 3,
            Code = "SKU-002",
            Name = "Linked to wrong item",
            Pcs = "vnt",
            Enabled = true,
            Balance = 1
        };

        var service = CreateService(db, product);

        var preview = await service.PreviewAsync(493);

        preview.Conflicts.Should().ContainSingle();
        preview.Conflicts[0].Reason.Should().Be("LinkedToDifferentItem");
    }

    [Fact]
    public async Task PreviewAsync_WhenProductHasValidPcsAndNoExistingLink_ShouldReturnToCreate()
    {
        await using var db = CreateDbContext();
        await SeedUnitOfMeasuresAsync(db, "vnt");

        var product = new AgnumProductDto
        {
            Id = 4,
            Code = "SKU-NEW",
            Name = "New Product",
            Pcs = "vnt",
            Enabled = true,
            Balance = 1
        };

        var service = CreateService(db, product);

        var preview = await service.PreviewAsync(493);

        preview.ToCreate.Should().ContainSingle(x => x.AgnumProductId == 4 && x.Code == "SKU-NEW");
    }

    [Fact]
    public async Task PreviewAsync_WhenExistingLinkExists_ShouldReturnToUpdate()
    {
        await using var db = CreateDbContext();
        await SeedUnitOfMeasuresAsync(db, "vnt");

        var existingItem = new Item { InternalSKU = "SKU-EXIST", Name = "Existing Item", BaseUoM = "vnt", CategoryId = await GetOrCreateCategoryIdAsync(db) };
        db.Items.Add(existingItem);
        await db.SaveChangesAsync();

        db.AgnumProductLinks.Add(new AgnumProductLink
        {
            ItemId = existingItem.Id,
            SndId = 493,
            AgnumProductId = 5,
            AgnumCode = "SKU-EXIST",
            AgnumEnabled = true,
            LastImportedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var product = new AgnumProductDto
        {
            Id = 5,
            Code = "SKU-EXIST",
            Name = "Existing Product",
            Pcs = "vnt",
            Enabled = true,
            Balance = 1
        };

        var service = CreateService(db, product);

        var preview = await service.PreviewAsync(493);

        preview.ToUpdate.Should().ContainSingle(x => x.AgnumProductId == 5);
    }

    [Fact]
    public async Task ApplyAsync_WhenProductHasMissingReferences_ShouldCreateReferenceGraph()
    {
        await using var db = CreateDbContext();

        var product = new AgnumProductDto
        {
            Id = 6,
            Code = "SKU-FULL",
            Name = "Full Import Product",
            Pcs = "m2",
            UnitOfMeasureType = "Length",
            Enabled = true,
            Balance = 1,
            Netto = 1.25m,
            Barcode = "111",
            Barcodes = new List<string> { "222" },
            Group = "Fabric",
            Category = "Cotton",
            Subgroup = "Printed",
            SupplierCode = "sup-001",
            SupplierName = "Supplier One",
            SupplierSku = "SUP-SKU-001"
        };

        var service = CreateService(db, product);

        var result = await service.ApplyAsync(493);

        result.Created.Should().Be(1);
        result.Skipped.Should().Be(0);

        var item = await db.Items.SingleAsync(x => x.InternalSKU == "SKU-FULL");
        item.BaseUoM.Should().Be("m2");
        item.Weight.Should().Be(1.25m);
        item.PrimaryBarcode.Should().Be("222");

        var unitOfMeasure = await db.UnitOfMeasures.SingleAsync(x => x.Code == "m2");
        unitOfMeasure.Type.Should().Be("Length");

        var leafCategory = await db.ItemCategories.SingleAsync(x => x.Code == "FABRIC-COTTON-PRINTED");
        item.CategoryId.Should().Be(leafCategory.Id);

        var supplier = await db.Suppliers.SingleAsync(x => x.Code == "SUP-001");
        supplier.Name.Should().Be("Supplier One");

        var mapping = await db.SupplierItemMappings.SingleAsync();
        mapping.SupplierId.Should().Be(supplier.Id);
        mapping.ItemId.Should().Be(item.Id);
        mapping.SupplierSKU.Should().Be("SUP-SKU-001");

        (await db.AgnumProductLinks.SingleAsync()).ItemId.Should().Be(item.Id);
        (await db.ItemExternalAttributes.CountAsync(x => x.ItemId == item.Id)).Should().BeGreaterThan(0);
        (await db.ItemBarcodes.CountAsync(x => x.ItemId == item.Id)).Should().Be(2);
    }

    [Fact]
    public async Task ApplyAsync_WhenProductsShareNewCategoryHierarchy_ShouldReuseTrackedCategories()
    {
        await using var db = CreateDbContext();
        await SeedUnitOfMeasuresAsync(db, "vnt");

        var first = new AgnumProductDto
        {
            Id = 7,
            Code = "SKU-CAT-1",
            Name = "First",
            Pcs = "vnt",
            Enabled = true,
            Balance = 1,
            Group = "Group",
            Category = "Category",
            Subgroup = "Subgroup"
        };
        var second = new AgnumProductDto
        {
            Id = 8,
            Code = "SKU-CAT-2",
            Name = "Second",
            Pcs = "vnt",
            Enabled = true,
            Balance = 1,
            Group = "Group",
            Category = "Category",
            Subgroup = "Subgroup"
        };

        var service = CreateService(db, first, second);

        var result = await service.ApplyAsync(493);

        result.Created.Should().Be(2);
        (await db.ItemCategories.CountAsync()).Should().Be(3);
        (await db.ItemCategories.CountAsync(x => x.Code == "GROUP-CATEGORY-SUBGROUP")).Should().Be(1);
    }

    [Fact]
    public async Task ApplyAsync_ShouldFetchProductsOnlyOnce()
    {
        await using var db = CreateDbContext();
        await SeedUnitOfMeasuresAsync(db, "vnt");

        var product = new AgnumProductDto
        {
            Id = 9,
            Code = "SKU-FETCH-ONCE",
            Name = "Fetch once",
            Pcs = "vnt",
            Enabled = true,
            Balance = 1
        };

        var clientMock = new Mock<IAgnumApiClient>();
        clientMock
            .Setup(x => x.GetProductsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AgnumProductDto> { product });
        clientMock
            .Setup(x => x.GetClientsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<AgnumClientDto>());

        var factoryMock = new Mock<IAgnumApiClientFactory>();
        factoryMock.Setup(x => x.GetForSndId(It.IsAny<int>())).Returns(clientMock.Object);

        var service = new AgnumNomenclatureImportService(
            db,
            factoryMock.Object,
            new LoggerFactory().CreateLogger<AgnumNomenclatureImportService>());

        await service.ApplyAsync(493);

        clientMock.Verify(x => x.GetProductsAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task ApplyAsync_WhenNettoIsNotPositive_ShouldStoreNullWeight(decimal netto)
    {
        await using var db = CreateDbContext();
        await SeedUnitOfMeasuresAsync(db, "vnt");

        var product = new AgnumProductDto
        {
            Id = 10,
            Code = $"SKU-WEIGHT-{netto}",
            Name = "Invalid weight",
            Pcs = "vnt",
            Enabled = true,
            Balance = 1,
            Netto = netto
        };

        var service = CreateService(db, product);

        await service.ApplyAsync(493);

        var item = await db.Items.SingleAsync(x => x.InternalSKU == product.Code);
        item.Weight.Should().BeNull();
    }

    [Fact]
    public async Task PreviewAsync_WhenAgnumPayloadContainsDuplicateCodes_ShouldReturnDuplicateAgnumCodeConflicts()
    {
        await using var db = CreateDbContext();
        await SeedUnitOfMeasuresAsync(db, "vnt");

        var first = new AgnumProductDto
        {
            Id = 11,
            Code = "SKU-DUP",
            Name = "Duplicate first",
            Pcs = "vnt",
            Enabled = true,
            Balance = 1
        };
        var second = new AgnumProductDto
        {
            Id = 12,
            Code = " sku-dup ",
            Name = "Duplicate second",
            Pcs = "vnt",
            Enabled = true,
            Balance = 1
        };

        var service = CreateService(db, first, second);

        var preview = await service.PreviewAsync(493);

        preview.ToCreate.Should().BeEmpty();
        preview.Conflicts.Should().HaveCount(2);
        preview.Conflicts.Should().OnlyContain(x => x.Reason == "DuplicateAgnumCode");
    }

    [Fact]
    public async Task ApplyAsync_WhenModifyDateHasUnspecifiedKind_ShouldStoreUtcAgnumModifiedAt()
    {
        await using var db = CreateDbContext();
        await SeedUnitOfMeasuresAsync(db, "vnt");

        var product = new AgnumProductDto
        {
            Id = 13,
            Code = "SKU-DATE",
            Name = "Date product",
            Pcs = "vnt",
            Enabled = true,
            Balance = 1,
            ModifyDate = new DateTime(2026, 5, 19, 10, 30, 0, DateTimeKind.Unspecified)
        };

        var service = CreateService(db, product);

        await service.ApplyAsync(493);

        var link = await db.AgnumProductLinks.SingleAsync(x => x.AgnumProductId == product.Id);
        link.AgnumModifiedAt.Should().NotBeNull();
        link.AgnumModifiedAt!.Value.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Fact]
    public async Task ApplyAsync_WhenAgnumPartnerClientsExist_ShouldImportSupplierAndCustomerCatalogs()
    {
        await using var db = CreateDbContext();
        await SeedUnitOfMeasuresAsync(db, "vnt");

        var product = new AgnumProductDto
        {
            Id = 14,
            Code = "SKU-SUPPLIER-CATALOG",
            Name = "Supplier catalog product",
            Pcs = "vnt",
            Enabled = true,
            Balance = 1
        };
        var supplier = new AgnumClientDto
        {
            Id = 57,
            Code = "PL7209-100-63-81",
            Name = "Samex P.P.H.U.",
            CompanyCode = "PL7209-100-63-81",
            VatCode = "PL72091006381",
            PozymNumbers = new List<int> { 1 }
        };
        var customer = new AgnumClientDto
        {
            Id = 6,
            Code = "110305282",
            Name = "Omnitel AB",
            Email = "test@example.com",
            RegisteredAddress = "T.Ševčenkos g.25, Vilnius",
            PozymNumbers = new List<int> { 2 }
        };
        var both = new AgnumClientDto
        {
            Id = 99,
            Code = "BOTH",
            Name = "Both Roles",
            ClientRoles = new List<string> { "BUYER", "SUPPLIER" }
        };

        var service = CreateService(db, new[] { product }, new[] { supplier, customer, both });

        await service.ApplyAsync(493);

        var importedSupplier = await db.Suppliers.SingleAsync(x => x.Code == "PL7209-100-63-81");
        importedSupplier.AgnumClientId.Should().Be(57);
        importedSupplier.Code.Should().Be("PL7209-100-63-81");
        importedSupplier.Name.Should().Be("Samex P.P.H.U.");
        importedSupplier.CompanyCode.Should().Be("PL7209-100-63-81");
        importedSupplier.VatCode.Should().Be("PL72091006381");
        importedSupplier.LastAgnumSyncedAt.Should().NotBeNull();
        (await db.Suppliers.CountAsync()).Should().Be(2);

        var importedCustomer = await db.Customers.SingleAsync(x => x.CustomerCode == "110305282");
        importedCustomer.AgnumClientId.Should().Be(6);
        importedCustomer.Name.Should().Be("Omnitel AB");
        importedCustomer.Email.Should().Be("test@example.com");
        importedCustomer.BillingAddress.Street.Should().Be("T.Ševčenkos g.25, Vilnius");
        (await db.Customers.CountAsync()).Should().Be(2);

        (await db.SupplierItemMappings.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task ApplyAsync_WhenSupplierLinkedToAgnum_ShouldNotBlankOutManualFields()
    {
        await using var db = CreateDbContext();
        await SeedUnitOfMeasuresAsync(db, "vnt");

        db.Suppliers.Add(new Supplier
        {
            AgnumClientId = 57,
            Code = "PL7209-100-63-81",
            Name = "Samex P.P.H.U.",
            Phone = "+370 600 11111",
            ContactName = "Manual Contact",
            Email = "manual@example.com",
            Website = "https://manual.example",
            CompanyCode = "OLD-COMPANY",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();

        var product = new AgnumProductDto
        {
            Id = 14,
            Code = "SKU-SUPPLIER-CATALOG",
            Name = "Supplier catalog product",
            Pcs = "vnt",
            Enabled = true,
            Balance = 1
        };
        var supplier = new AgnumClientDto
        {
            Id = 57,
            Code = "PL7209-100-63-81",
            Name = "Samex P.P.H.U.",
            CompanyCode = "PL7209-100-63-81",
            VatCode = "PL72091006381",
            Email = "", // blank from Agnum must NOT clear the manual value
            PozymNumbers = new List<int> { 1 }
        };

        var service = CreateService(db, new[] { product }, new[] { supplier });

        await service.ApplyAsync(493);

        var updated = await db.Suppliers.SingleAsync(x => x.Code == "PL7209-100-63-81");
        // Agnum-owned fields are refreshed.
        updated.CompanyCode.Should().Be("PL7209-100-63-81");
        updated.VatCode.Should().Be("PL72091006381");
        updated.LastAgnumSyncedAt.Should().NotBeNull();
        // Manual values that Agnum does not provide (or sends blank) are preserved.
        updated.Email.Should().Be("manual@example.com");
        updated.Phone.Should().Be("+370 600 11111");
        updated.ContactName.Should().Be("Manual Contact");
        updated.Website.Should().Be("https://manual.example");
    }

    private static AgnumNomenclatureImportService CreateService(WarehouseDbContext db, params AgnumProductDto[] products)
        => CreateService(db, products, Array.Empty<AgnumClientDto>());

    private static AgnumNomenclatureImportService CreateService(
        WarehouseDbContext db,
        IReadOnlyList<AgnumProductDto> products,
        IReadOnlyList<AgnumClientDto> suppliers)
    {
        var clientMock = new Mock<IAgnumApiClient>();
        clientMock.Setup(x => x.GetProductsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(products.ToList());
        clientMock.Setup(x => x.GetClientsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(suppliers.ToList());

        var factoryMock = new Mock<IAgnumApiClientFactory>();
        factoryMock.Setup(x => x.GetForSndId(It.IsAny<int>())).Returns(clientMock.Object);

        return new AgnumNomenclatureImportService(db, factoryMock.Object, new LoggerFactory().CreateLogger<AgnumNomenclatureImportService>());
    }

    private static WarehouseDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<WarehouseDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new WarehouseDbContext(options);
    }

    private static async Task<int> GetOrCreateCategoryIdAsync(WarehouseDbContext db)
    {
        if (await db.ItemCategories.AnyAsync())
        {
            return await db.ItemCategories.Select(x => x.Id).FirstAsync();
        }

        var category = new ItemCategory { Code = "DEFAULT", Name = "Default" };
        db.ItemCategories.Add(category);
        await db.SaveChangesAsync();
        return category.Id;
    }

    private static async Task SeedUnitOfMeasuresAsync(WarehouseDbContext db, params string[] codes)
    {
        foreach (var code in codes)
        {
            db.UnitOfMeasures.Add(new UnitOfMeasure { Code = code, Name = code, Type = "Piece" });
        }

        await db.SaveChangesAsync();
    }
}

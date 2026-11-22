using FluentAssertions;
using LKvitai.MES.Modules.Warehouse.Api.Controllers;
using LKvitai.MES.Modules.Warehouse.Api.Security;
using LKvitai.MES.Modules.Warehouse.Domain.Entities;
using LKvitai.MES.Modules.Warehouse.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace LKvitai.MES.Tests.Warehouse.Unit;

public class BarcodesSupplierCodeLookupTests
{
    [Fact]
    public async Task Lookup_WhenBarcodeMatches_ShouldReturnItemAndIgnoreSupplierCode()
    {
        await using var db = CreateDbContext();
        var (bolt, nut) = await SeedItemsAsync(db);
        db.ItemBarcodes.Add(new ItemBarcode { ItemId = bolt.Id, Barcode = "CODE-1", BarcodeType = "EAN13", IsPrimary = true });
        db.SupplierItemMappings.Add(new SupplierItemMapping { SupplierId = 1, ItemId = nut.Id, SupplierSKU = "CODE-1" });
        await db.SaveChangesAsync();

        var result = await CreateController(db).LookupAsync("CODE-1", null);

        var dto = result.Should().BeOfType<OkObjectResult>().Subject.Value
            .Should().BeOfType<BarcodesController.ItemBarcodeLookupDto>().Subject;
        dto.ItemId.Should().Be(bolt.Id);
        dto.MatchedBySupplierCode.Should().BeFalse();
    }

    [Fact]
    public async Task Lookup_WhenNoBarcodeAndSingleSupplierCodeMatch_ShouldReturnItemMarkedAsSupplierCode()
    {
        await using var db = CreateDbContext();
        var (bolt, _) = await SeedItemsAsync(db);
        db.SupplierItemMappings.Add(new SupplierItemMapping { SupplierId = 1, ItemId = bolt.Id, SupplierSKU = "Sup-77" });
        await db.SaveChangesAsync();

        var result = await CreateController(db).LookupAsync("  sup-77 ", null);

        var dto = result.Should().BeOfType<OkObjectResult>().Subject.Value
            .Should().BeOfType<BarcodesController.ItemBarcodeLookupDto>().Subject;
        dto.ItemId.Should().Be(bolt.Id);
        dto.InternalSKU.Should().Be("RM-0001");
        dto.MatchedBySupplierCode.Should().BeTrue();
    }

    [Fact]
    public async Task Lookup_WhenSameCodeMapsTwoSuppliersToSameItem_ShouldReturnItem()
    {
        await using var db = CreateDbContext();
        var (bolt, _) = await SeedItemsAsync(db);
        db.SupplierItemMappings.AddRange(
            new SupplierItemMapping { SupplierId = 1, ItemId = bolt.Id, SupplierSKU = "X-1" },
            new SupplierItemMapping { SupplierId = 2, ItemId = bolt.Id, SupplierSKU = "X-1" });
        await db.SaveChangesAsync();

        var result = await CreateController(db).LookupAsync("X-1", null);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task Lookup_WhenSupplierCodeMatchesSeveralItems_ShouldReturnConflictWithCandidates()
    {
        await using var db = CreateDbContext();
        var (bolt, nut) = await SeedItemsAsync(db);
        db.SupplierItemMappings.AddRange(
            new SupplierItemMapping { SupplierId = 1, ItemId = bolt.Id, SupplierSKU = "SHARED" },
            new SupplierItemMapping { SupplierId = 2, ItemId = nut.Id, SupplierSKU = "shared" });
        await db.SaveChangesAsync();

        var result = await CreateController(db).LookupAsync("SHARED", null);

        var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status409Conflict);
        var problem = objectResult.Value.Should().BeOfType<ProblemDetails>().Subject;
        problem.Extensions["errorCode"].Should().Be(BarcodesController.SupplierCodeAmbiguousErrorCode);
        var candidates = problem.Extensions["candidates"]
            .Should().BeAssignableTo<IEnumerable<BarcodesController.SupplierCodeCandidateDto>>().Subject.ToList();
        candidates.Select(x => x.ItemId).Should().BeEquivalentTo(new[] { bolt.Id, nut.Id });
    }

    [Fact]
    public async Task Lookup_WhenNothingMatches_ShouldReturnNotFound()
    {
        await using var db = CreateDbContext();
        await SeedItemsAsync(db);

        var result = await CreateController(db).LookupAsync("MISSING", null);

        result.Should().BeOfType<ObjectResult>().Subject.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }

    private static BarcodesController CreateController(WarehouseDbContext db)
        => new(db)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

    private static async Task<(Item Bolt, Item Nut)> SeedItemsAsync(WarehouseDbContext db)
    {
        db.Suppliers.AddRange(
            new Supplier { Id = 1, Code = "S1", Name = "Supplier One" },
            new Supplier { Id = 2, Code = "S2", Name = "Supplier Two" });
        var bolt = new Item { InternalSKU = "RM-0001", Name = "Bolt", BaseUoM = "PCS", CategoryId = 1 };
        var nut = new Item { InternalSKU = "RM-0002", Name = "Nut", BaseUoM = "PCS", CategoryId = 1 };
        db.Items.AddRange(bolt, nut);
        await db.SaveChangesAsync();
        return (bolt, nut);
    }

    private static WarehouseDbContext CreateDbContext()
        => new(new DbContextOptionsBuilder<WarehouseDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
}

public class SupplierItemMappingsControllerDeleteTests
{
    [Fact]
    public void Delete_ShouldRequireSamePolicyAsUpdate()
    {
        static AuthorizeAttribute Policy(string method) => typeof(SupplierItemMappingsController)
            .GetMethod(method)!
            .GetCustomAttributes(typeof(AuthorizeAttribute), true)
            .OfType<AuthorizeAttribute>()
            .Single();

        Policy(nameof(SupplierItemMappingsController.DeleteAsync)).Policy.Should().Be(WarehousePolicies.ManagerOrAdmin);
        Policy(nameof(SupplierItemMappingsController.DeleteAsync)).Policy
            .Should().Be(Policy(nameof(SupplierItemMappingsController.UpdateAsync)).Policy);
    }

    [Fact]
    public async Task Delete_WhenMappingExists_ShouldRemoveItAndReturnNoContent()
    {
        await using var db = CreateDbContext();
        db.SupplierItemMappings.Add(new SupplierItemMapping { Id = 5, SupplierId = 1, ItemId = 1, SupplierSKU = "A" });
        db.SupplierItemMappings.Add(new SupplierItemMapping { Id = 6, SupplierId = 1, ItemId = 1, SupplierSKU = "B" });
        await db.SaveChangesAsync();

        var result = await CreateController(db).DeleteAsync(5);

        result.Should().BeOfType<NoContentResult>();
        (await db.SupplierItemMappings.Select(x => x.Id).ToListAsync()).Should().Equal(6);
    }

    [Fact]
    public async Task Delete_WhenMappingMissing_ShouldReturnNotFound()
    {
        await using var db = CreateDbContext();

        var result = await CreateController(db).DeleteAsync(99);

        result.Should().BeOfType<ObjectResult>().Subject.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }

    private static SupplierItemMappingsController CreateController(WarehouseDbContext db)
        => new(db)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

    private static WarehouseDbContext CreateDbContext()
        => new(new DbContextOptionsBuilder<WarehouseDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
}

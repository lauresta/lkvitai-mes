using FluentAssertions;
using LKvitai.MES.Api.Controllers;
using LKvitai.MES.Application.Ports;
using LKvitai.MES.Modules.Warehouse.Domain.Entities;
using LKvitai.MES.Infrastructure.Persistence;
using Marten;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using System.Text;
using Xunit;

namespace LKvitai.MES.Tests.Unit;

public class Visualization3dTests
{
    [Fact]
    [Trait("Category", "3DVisualization")]
    public async Task CreateAsync_WithInvalidZoneType_ShouldReturnBadRequest()
    {
        await using var db = CreateDbContext();
        var controller = new LocationsController(db)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        var response = await controller.CreateAsync(
            new LocationsController.UpsertLocationRequest(
                "R1-C1-L1",
                "LOC-101",
                "Bin",
                null,
                false,
                null,
                null,
                "Active",
                "InvalidZone",
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null));

        response.Should().BeOfType<ObjectResult>();
        ((ObjectResult)response).StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    [Trait("Category", "3DVisualization")]
    public async Task CreateAsync_WithLowercaseZoneType_ShouldNormalizeToCanonicalValue()
    {
        await using var db = CreateDbContext();
        var controller = new LocationsController(db)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        var response = await controller.CreateAsync(
            new LocationsController.UpsertLocationRequest(
                "R1-C1-L2",
                "LOC-102",
                "Bin",
                null,
                false,
                null,
                null,
                "Active",
                "general",
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null));

        response.Should().BeOfType<CreatedResult>();
        var created = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.SingleAsync(db.Locations);
        created.ZoneType.Should().Be("General");
    }

    [Fact]
    [Trait("Category", "3DVisualization")]
    public async Task PutLayoutAsync_ShouldPersistLayoutAndZones()
    {
        await using var db = CreateDbContext();
        var controller = CreateVisualizationController(db);

        var response = await controller.PutLayoutAsync(new WarehouseVisualizationController.UpsertWarehouseLayoutRequest(
            "Main",
            50m,
            100m,
            10m,
            new[]
            {
                new WarehouseVisualizationController.UpsertZoneRequest("RECEIVING", 0m, 0m, 10m, 100m, "#ADD8E6")
            }));

        response.Should().BeOfType<OkObjectResult>();
        var payload = ((OkObjectResult)response).Value.Should().BeOfType<WarehouseVisualizationController.LayoutResponse>().Subject;
        payload.WarehouseCode.Should().Be("Main");
        payload.Zones.Should().HaveCount(1);

        var layout = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.SingleAsync(
            db.WarehouseLayouts.Include(x => x.Zones));
        layout.WidthMeters.Should().Be(50m);
        layout.LengthMeters.Should().Be(100m);
        layout.HeightMeters.Should().Be(10m);
        layout.Zones.Should().ContainSingle(x => x.ZoneType == "RECEIVING");
    }

    [Fact]
    [Trait("Category", "3DVisualization")]
    public async Task GetLayoutAsync_WhenMissing_ShouldDeriveDimensionsFromCoordinates()
    {
        await using var db = CreateDbContext();
        db.Locations.Add(new Location
        {
            Code = "R3-C6-L3",
            Barcode = "LOC-001",
            Type = "Bin",
            Status = "Active",
            CoordinateX = 15.5m,
            CoordinateY = 32m,
            CoordinateZ = 6m
        });
        await db.SaveChangesAsync();

        var controller = CreateVisualizationController(db);
        var response = await controller.GetLayoutAsync("Main");

        response.Should().BeOfType<OkObjectResult>();
        var payload = ((OkObjectResult)response).Value.Should().BeOfType<WarehouseVisualizationController.LayoutResponse>().Subject;
        payload.WidthMeters.Should().Be(16.5m);
        payload.LengthMeters.Should().Be(33m);
        payload.HeightMeters.Should().Be(7m);
    }

    [Fact]
    [Trait("Category", "3DVisualization")]
    public async Task UpdateByCodeAsync_WhenCoordinatesOverlap_ShouldReturnBadRequest()
    {
        await using var db = CreateDbContext();
        db.Locations.AddRange(
            new Location
            {
                Code = "R1-C1-L1",
                Barcode = "LOC-001",
                Type = "Bin",
                Status = "Active",
                CoordinateX = 5m,
                CoordinateY = 10m,
                CoordinateZ = 2m
            },
            new Location
            {
                Code = "R2-C2-L2",
                Barcode = "LOC-002",
                Type = "Bin",
                Status = "Active",
                CoordinateX = 10m,
                CoordinateY = 20m,
                CoordinateZ = 4m
            });
        await db.SaveChangesAsync();

        var controller = new LocationsController(db)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        var response = await controller.UpdateByCodeAsync(
            "R2-C2-L2",
            new LocationsController.UpdateCoordinatesRequest(
                5m,
                10m,
                2m,
                null,
                null,
                null,
                "R2",
                "C2",
                "L2",
                "B1",
                1000m,
                2m));

        response.Should().BeOfType<ObjectResult>();
        ((ObjectResult)response).StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    [Trait("Category", "3DVisualization")]
    public async Task UpdateByCodeAsync_ShouldPersistCoordinates()
    {
        await using var db = CreateDbContext();
        db.Locations.Add(new Location
        {
            Code = "R3-C6-L3",
            Barcode = "LOC-003",
            Type = "Bin",
            Status = "Active"
        });
        await db.SaveChangesAsync();

        var controller = new LocationsController(db)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        var response = await controller.UpdateByCodeAsync(
            "R3-C6-L3",
            new LocationsController.UpdateCoordinatesRequest(
                15.5m,
                32m,
                6m,
                null,
                null,
                null,
                "R3",
                "C6",
                "L3",
                "B3",
                1000m,
                2m));

        response.Should().BeOfType<OkObjectResult>();

        var updated = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.SingleAsync(db.Locations);
        updated.CoordinateX.Should().Be(15.5m);
        updated.CoordinateY.Should().Be(32m);
        updated.CoordinateZ.Should().Be(6m);
        updated.CapacityWeight.Should().Be(1000m);
        updated.CapacityVolume.Should().Be(2m);
    }

    [Fact]
    [Trait("Category", "3DVisualization")]
    public async Task BulkCoordinatesAsync_ShouldUpdateLocations()
    {
        await using var db = CreateDbContext();
        db.Locations.AddRange(
            new Location { Code = "R1-C1-L1", Barcode = "LOC-001", Type = "Bin", Status = "Active" },
            new Location { Code = "R2-C2-L2", Barcode = "LOC-002", Type = "Bin", Status = "Active" });
        await db.SaveChangesAsync();

        var controller = new LocationsController(db)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        var csv =
            "LocationCode,X,Y,Z,CapacityWeight,CapacityVolume\n" +
            "R1-C1-L1,10,20,2,1000,1.5\n" +
            "R2-C2-L2,15,30,3,800,1.2\n";
        var file = BuildCsvFormFile(csv);

        var response = await controller.BulkCoordinatesAsync(file);

        response.Should().BeOfType<OkObjectResult>();
        var payload = ((OkObjectResult)response).Value.Should()
            .BeOfType<LocationsController.BulkCoordinatesResponse>().Subject;
        payload.SuccessCount.Should().Be(2);
        payload.ErrorCount.Should().Be(0);

        var locations = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToListAsync(
            db.Locations.OrderBy(x => x.Code));
        locations[0].CoordinateX.Should().Be(10m);
        locations[0].CoordinateY.Should().Be(20m);
        locations[0].CoordinateZ.Should().Be(2m);
        locations[1].CoordinateX.Should().Be(15m);
        locations[1].CoordinateY.Should().Be(30m);
        locations[1].CoordinateZ.Should().Be(3m);
    }

    [Fact]
    [Trait("Category", "3DVisualization")]
    public async Task BulkCoordinatesAsync_WhenNegativeCoordinate_ShouldReturnError()
    {
        await using var db = CreateDbContext();
        db.Locations.Add(new Location
        {
            Code = "R1-C1-L1",
            Barcode = "LOC-001",
            Type = "Bin",
            Status = "Active"
        });
        await db.SaveChangesAsync();

        var controller = new LocationsController(db)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        var csv =
            "LocationCode,X,Y,Z,CapacityWeight,CapacityVolume\n" +
            "R1-C1-L1,-1,20,2,1000,1.5\n";
        var file = BuildCsvFormFile(csv);

        var response = await controller.BulkCoordinatesAsync(file);

        response.Should().BeOfType<OkObjectResult>();
        var payload = ((OkObjectResult)response).Value.Should()
            .BeOfType<LocationsController.BulkCoordinatesResponse>().Subject;
        payload.SuccessCount.Should().Be(0);
        payload.ErrorCount.Should().Be(1);
        payload.Errors.Single().Should().Contain("Coordinates must be >= 0");
    }

    private static WarehouseDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<WarehouseDbContext>()
            .UseInMemoryDatabase($"visualization3d-tests-{Guid.NewGuid():N}")
            .Options;
        return new WarehouseDbContext(options);
    }

    private static WarehouseVisualizationController CreateVisualizationController(WarehouseDbContext db)
    {
        var documentStore = new Mock<IDocumentStore>(MockBehavior.Strict);
        var hardLocks = new Mock<IActiveHardLocksRepository>(MockBehavior.Strict);
        var logger = new Mock<ILogger<WarehouseVisualizationController>>();

        return new WarehouseVisualizationController(
            db,
            documentStore.Object,
            hardLocks.Object,
            logger.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };
    }

    private static IFormFile BuildCsvFormFile(string content)
    {
        var bytes = Encoding.UTF8.GetBytes(content);
        var stream = new MemoryStream(bytes);
        return new FormFile(stream, 0, bytes.Length, "file", "coordinates.csv")
        {
            Headers = new HeaderDictionary(),
            ContentType = "text/csv"
        };
    }
}

using FluentAssertions;
using LKvitai.MES.Modules.Warehouse.Domain.Entities;
using LKvitai.MES.Modules.Warehouse.Infrastructure.Persistence;
using LKvitai.MES.Modules.Warehouse.Infrastructure.Visualization;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace LKvitai.MES.Tests.Warehouse.Unit;

public sealed class RackVisualizationGeometryTests
{
    [Fact]
    public void Calculate_WithPalletRackPlacement_ShouldProduceSlotsAndInsetBinGeometry()
    {
        var validator = new RackLayoutValidator();
        var calculator = new WarehouseGeometryCalculator();
        var layout = BuildLayout();
        var document = validator.Parse("""
            {
              "warehouseCode": "2",
              "racks": [
                {
                  "id": "C",
                  "type": "PalletRack",
                  "origin": { "x": 5.0, "y": 8.0, "z": 0.0 },
                  "dimensions": { "width": 12.0, "depth": 1.1, "height": 5.4 },
                  "orientationDeg": 0,
                  "slotsPerLevel": 6,
                  "bayCount": 6,
                  "backToBack": false,
                  "pairedWithRackId": null,
                  "levels": [
                    { "index": 1, "heightFromBase": 0.10 },
                    { "index": 2, "heightFromBase": 1.80 },
                    { "index": 3, "heightFromBase": 3.50 }
                  ]
                }
              ]
            }
            """);
        var locations = new[]
        {
            new Location
            {
                Id = 1,
                Code = "BIN-001",
                Barcode = "BIN-001",
                Type = "Bin",
                Status = "Active",
                RackRowId = "C",
                ShelfLevelIndex = 2,
                SlotStart = 2,
                SlotSpan = 2,
                LocationRole = "Cell"
            }
        };

        var result = calculator.Calculate("2", document, locations);

        result.Racks.Should().ContainSingle();
        result.Slots.Should().HaveCount(18);
        result.Slots.Count(x => x.Occupied).Should().Be(2);

        var slot = result.Slots.Single(x => x.Address == "2-C2-2");
        slot.Occupied.Should().BeTrue();

        var bin = result.Bins.Single();
        bin.Address.Should().Be("2-C2-2+2");
        bin.RackId.Should().Be("C");
        bin.Level.Should().Be(2);
        bin.StartSlot.Should().Be(2);
        bin.Span.Should().Be(2);
        bin.Width.Should().Be(3.92m);
        bin.Length.Should().Be(1.02m);
        bin.Height.Should().Be(1.62m);
        bin.X.Should().Be(7.04m);
        bin.Y.Should().Be(8.04m);
        bin.Z.Should().Be(1.84m);
    }

    [Fact]
    public void Calculate_WithLegacyBin_ShouldFallbackToCoordinates()
    {
        var calculator = new WarehouseGeometryCalculator();

        var result = calculator.Calculate(
            "Main",
            RackLayoutDocument.Empty,
            new[]
            {
                new Location
                {
                    Id = 7,
                    Code = "LEGACY-01",
                    Barcode = "LEGACY-01",
                    Type = "Bin",
                    Status = "Active",
                    CoordinateX = 1.5m,
                    CoordinateY = 2.5m,
                    CoordinateZ = 3.5m,
                    WidthMeters = 1m,
                    LengthMeters = 1.2m,
                    HeightMeters = 1.4m
                }
            });

        var bin = result.Bins.Single();
        bin.Address.Should().BeNull();
        bin.X.Should().Be(1.5m);
        bin.Y.Should().Be(2.5m);
        bin.Z.Should().Be(3.5m);
        bin.Width.Should().Be(1m);
        bin.Length.Should().Be(1.2m);
        bin.Height.Should().Be(1.4m);
    }

    [Fact]
    public async Task ValidateAsync_WithOverlappingPlacement_ShouldReturnError()
    {
        await using var db = CreateDbContext();
        db.WarehouseLayouts.Add(BuildLayout("""
            {
              "warehouseCode": "2",
              "racks": [
                {
                  "id": "C",
                  "type": "PalletRack",
                  "origin": { "x": 5.0, "y": 8.0, "z": 0.0 },
                  "dimensions": { "width": 12.0, "depth": 1.1, "height": 5.4 },
                  "orientationDeg": 0,
                  "slotsPerLevel": 6,
                  "bayCount": 6,
                  "backToBack": false,
                  "pairedWithRackId": null,
                  "levels": [
                    { "index": 1, "heightFromBase": 0.10 },
                    { "index": 2, "heightFromBase": 1.80 }
                  ]
                }
              ]
            }
            """));
        db.Locations.AddRange(
            new Location
            {
                Id = 1,
                Code = "BIN-001",
                Barcode = "BIN-001",
                Type = "Bin",
                Status = "Active",
                RackRowId = "C",
                ShelfLevelIndex = 2,
                SlotStart = 2,
                SlotSpan = 2
            },
            new Location
            {
                Id = 2,
                Code = "BIN-002",
                Barcode = "BIN-002",
                Type = "Bin",
                Status = "Active"
            });
        await db.SaveChangesAsync();

        var sut = new BinPlacementValidator(db, new RackLayoutValidator());

        var (_, error) = await sut.ValidateAsync(
            2,
            new RackPlacementRequest("2", "C", 2, 3, 1, "Cell"));

        error.Should().Contain("overlaps");
    }

    [Fact]
    public async Task ValidateAsync_WithFloorStoragePlacement_ShouldReturnError()
    {
        await using var db = CreateDbContext();
        db.WarehouseLayouts.Add(BuildLayout("""
            {
              "warehouseCode": "2",
              "racks": [
                {
                  "id": "FLOOR-A",
                  "type": "FloorStorage",
                  "origin": { "x": 10.0, "y": 4.0, "z": 0.0 },
                  "dimensions": { "width": 8.0, "depth": 6.0, "height": 2.0 },
                  "orientationDeg": 0,
                  "slotsPerLevel": 0,
                  "bayCount": 0,
                  "backToBack": false,
                  "pairedWithRackId": null,
                  "levels": []
                }
              ]
            }
            """));
        db.Locations.Add(new Location
        {
            Id = 10,
            Code = "BIN-010",
            Barcode = "BIN-010",
            Type = "Bin",
            Status = "Active"
        });
        await db.SaveChangesAsync();

        var sut = new BinPlacementValidator(db, new RackLayoutValidator());

        var (_, error) = await sut.ValidateAsync(
            10,
            new RackPlacementRequest("2", "FLOOR-A", 1, 1, 1, null));

        error.Should().Contain("does not support slot placement");
    }

    private static WarehouseLayout BuildLayout(string? racksJson = null)
    {
        return new WarehouseLayout
        {
            WarehouseCode = "2",
            WidthMeters = 60m,
            LengthMeters = 60m,
            HeightMeters = 10m,
            RacksJson = racksJson,
            UpdatedAt = DateTimeOffset.UtcNow
        };
    }

    private static WarehouseDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<WarehouseDbContext>()
            .UseInMemoryDatabase($"rack-geometry-tests-{Guid.NewGuid():N}")
            .Options;
        return new WarehouseDbContext(options);
    }
}

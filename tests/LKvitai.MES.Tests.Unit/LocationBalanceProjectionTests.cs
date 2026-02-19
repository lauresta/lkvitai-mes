using FluentAssertions;
using LKvitai.MES.Contracts.Events;
using LKvitai.MES.Contracts.ReadModels;
using LKvitai.MES.Projections;
using Xunit;

namespace LKvitai.MES.Tests.Unit;

/// <summary>
/// Unit tests for LocationBalance projection logic
/// Tests projection apply logic without Marten infrastructure
/// </summary>
public class LocationBalanceProjectionTests
{
    [Fact]
    public void Apply_IncreasesBalance_ForToLocation()
    {
        // Arrange
        var streamId = "stock-ledger:MAIN:BIN-001:SKU933";
        var evt = new StockMovedEvent
        {
            MovementId = Guid.NewGuid(),
            SKU = "SKU933",
            Quantity = 100m,
            FromLocation = "SUPPLIER",
            ToLocation = "BIN-001",
            MovementType = "RECEIPT",
            OperatorId = Guid.NewGuid(),
            Timestamp = DateTime.UtcNow
        };
        
        var view = new LocationBalanceView
        {
            Id = "MAIN:BIN-001:SKU933",
            WarehouseId = "MAIN",
            Location = "BIN-001",
            SKU = "SKU933",
            Quantity = 0,
            LastUpdated = DateTime.UtcNow.AddHours(-1)
        };
        
        // Act
        var result = LocationBalanceAggregation.Apply(evt, view, streamId);
        
        // Assert
        result.Quantity.Should().Be(100m);
        result.LastUpdated.Should().Be(evt.Timestamp);
    }
    
    [Fact]
    public void Apply_DecreasesBalance_ForFromLocation()
    {
        // Arrange
        var streamId = "stock-ledger:MAIN:BIN-001:SKU933";
        var evt = new StockMovedEvent
        {
            MovementId = Guid.NewGuid(),
            SKU = "SKU933",
            Quantity = 30m,
            FromLocation = "BIN-001",
            ToLocation = "PRODUCTION",
            MovementType = "PICK",
            OperatorId = Guid.NewGuid(),
            Timestamp = DateTime.UtcNow
        };
        
        var view = new LocationBalanceView
        {
            Id = "MAIN:BIN-001:SKU933",
            WarehouseId = "MAIN",
            Location = "BIN-001",
            SKU = "SKU933",
            Quantity = 100m,
            LastUpdated = DateTime.UtcNow.AddHours(-1)
        };
        
        // Act
        var result = LocationBalanceAggregation.Apply(evt, view, streamId);
        
        // Assert
        result.Quantity.Should().Be(70m);
        result.LastUpdated.Should().Be(evt.Timestamp);
    }
    
    [Fact]
    public void Apply_HandlesTransfer_BetweenTwoLocations()
    {
        // Arrange - movement from BIN-001 to BIN-002
        var streamId = "stock-ledger:MAIN:BIN-001:SKU933";
        var evt = new StockMovedEvent
        {
            MovementId = Guid.NewGuid(),
            SKU = "SKU933",
            Quantity = 50m,
            FromLocation = "BIN-001",
            ToLocation = "BIN-002",
            MovementType = "TRANSFER",
            OperatorId = Guid.NewGuid(),
            Timestamp = DateTime.UtcNow
        };
        
        var fromView = new LocationBalanceView
        {
            Id = "MAIN:BIN-001:SKU933",
            WarehouseId = "MAIN",
            Location = "BIN-001",
            SKU = "SKU933",
            Quantity = 100m,
            LastUpdated = DateTime.UtcNow.AddHours(-1)
        };
        
        var toView = new LocationBalanceView
        {
            Id = "MAIN:BIN-002:SKU933",
            WarehouseId = "MAIN",
            Location = "BIN-002",
            SKU = "SKU933",
            Quantity = 0m,
            LastUpdated = DateTime.UtcNow.AddHours(-1)
        };
        
        // Act
        var fromResult = LocationBalanceAggregation.Apply(evt, fromView, streamId);
        var toResult = LocationBalanceAggregation.Apply(evt, toView, streamId);
        
        // Assert
        fromResult.Quantity.Should().Be(50m, "FROM location should decrease");
        toResult.Quantity.Should().Be(50m, "TO location should increase");
    }
    
    [Fact]
    public void Apply_UsesOnlyEventData_NoExternalQueries()
    {
        // This test validates V-5 Rule B: Self-contained event data
        // The Apply method should work with ONLY the event data
        
        // Arrange
        var streamId = "stock-ledger:MAIN:BIN-001:SKU933";
        var evt = new StockMovedEvent
        {
            MovementId = Guid.NewGuid(),
            SKU = "SKU933",
            Quantity = 25m,
            FromLocation = "BIN-001",
            ToLocation = "BIN-002",
            MovementType = "TRANSFER",
            OperatorId = Guid.NewGuid(),
            Timestamp = DateTime.UtcNow
        };
        
        var view = new LocationBalanceView
        {
            Id = "MAIN:BIN-001:SKU933",
            WarehouseId = "MAIN",
            Location = "BIN-001",
            SKU = "SKU933",
            Quantity = 100m,
            LastUpdated = DateTime.UtcNow.AddHours(-1)
        };
        
        // Act - should not throw or require external data
        LocationBalanceView? result = null;
        var act = () => result = LocationBalanceAggregation.Apply(evt, view, streamId);
        
        // Assert - completes without external dependencies
        act.Should().NotThrow();
        result!.Quantity.Should().Be(75m);
    }
}

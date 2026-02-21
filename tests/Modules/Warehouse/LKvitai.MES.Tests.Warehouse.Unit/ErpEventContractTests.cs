using System.Text.Json;
using FluentAssertions;
using LKvitai.MES.Contracts.Events;
using Xunit;

namespace LKvitai.MES.Tests.Warehouse.Unit;

public class ErpEventContractTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    [Fact]
    [Trait("Category", "Contract")]
    public void ShipmentDispatchedEvent_ShouldPreserveContractFields()
    {
        var evt = new ShipmentDispatchedEvent
        {
            ShipmentId = Guid.NewGuid(),
            ShipmentNumber = "SHP-100",
            OutboundOrderId = Guid.NewGuid(),
            OutboundOrderNumber = "SO-100",
            Carrier = "FEDEX",
            TrackingNumber = "FDX-001",
            VehicleId = "TRUCK-1",
            DispatchedAt = DateTime.UtcNow,
            DispatchedBy = "dispatcher",
            ManualTracking = false,
            SchemaVersion = "v1"
        };

        var json = JsonSerializer.Serialize(evt, JsonOptions);
        var clone = JsonSerializer.Deserialize<ShipmentDispatchedEvent>(json, JsonOptions);

        clone.Should().NotBeNull();
        clone!.SchemaVersion.Should().Be("v1");
        clone.ShipmentNumber.Should().Be("SHP-100");
        clone.OutboundOrderNumber.Should().Be("SO-100");
        clone.TrackingNumber.Should().Be("FDX-001");
    }

    [Fact]
    [Trait("Category", "Contract")]
    public void StockMovedEvent_ShouldPreserveSchemaVersion()
    {
        var evt = new StockMovedEvent
        {
            SKU = "RM-0010",
            Quantity = 5,
            FromLocation = "A1",
            ToLocation = "B1",
            MovementType = "ERP_ISSUE",
            OperatorId = Guid.NewGuid(),
            Reason = "Material issue",
            SchemaVersion = "v1"
        };

        var json = JsonSerializer.Serialize(evt, JsonOptions);
        var clone = JsonSerializer.Deserialize<StockMovedEvent>(json, JsonOptions);

        clone.Should().NotBeNull();
        clone!.SchemaVersion.Should().Be("v1");
        clone.MovementType.Should().Be("ERP_ISSUE");
        clone.Reason.Should().Be("Material issue");
    }
}

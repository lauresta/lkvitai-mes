using System.Text.Json;
using FluentAssertions;
using LKvitai.MES.Contracts.Events;
using Xunit;

namespace LKvitai.MES.Tests.Unit;

public class MasterDataOperationalEventsSerializationTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    [Fact]
    public void AllMasterDataOperationalEvents_ShouldRoundTripJson()
    {
        var now = DateTime.UtcNow;

        var events = new WarehouseOperationalEvent[]
        {
            new GoodsReceivedEvent
            {
                AggregateId = Guid.NewGuid(),
                UserId = "operator-1",
                TraceId = "trace-a",
                ShipmentId = Guid.NewGuid(),
                LineId = 10,
                ItemId = 42,
                SKU = "RM-0001",
                ReceivedQty = 100,
                BaseUoM = "PCS",
                DestinationLocation = "QC_HOLD",
                LotNumber = "LOT-001",
                Timestamp = now
            },
            new StockAdjustedEvent
            {
                AggregateId = Guid.NewGuid(),
                UserId = "manager-1",
                TraceId = "trace-b",
                AdjustmentId = Guid.NewGuid(),
                ItemId = 42,
                SKU = "RM-0001",
                Location = "A-01",
                QtyDelta = -5,
                ReasonCode = "DAMAGE",
                Timestamp = now
            },
            new PickCompletedEvent
            {
                AggregateId = Guid.NewGuid(),
                UserId = "operator-2",
                TraceId = "trace-c",
                PickTaskId = Guid.NewGuid(),
                OrderId = "ORD-1",
                ItemId = 42,
                SKU = "RM-0001",
                PickedQty = 25,
                FromLocation = "A-01",
                ToLocation = "SHIPPING",
                Timestamp = now
            },
            new ReservationCreatedMasterDataEvent
            {
                AggregateId = Guid.NewGuid(),
                UserId = "system",
                TraceId = "trace-d",
                ReservationId = Guid.NewGuid(),
                ItemId = 42,
                SKU = "RM-0001",
                ReservedQty = 25,
                OrderId = "ORD-1",
                Location = "A-01",
                ExpiresAt = DateTimeOffset.UtcNow.AddHours(1),
                Timestamp = now
            },
            new ReservationReleasedMasterDataEvent
            {
                AggregateId = Guid.NewGuid(),
                UserId = "system",
                TraceId = "trace-e",
                ReservationId = Guid.NewGuid(),
                ItemId = 42,
                SKU = "RM-0001",
                ReleasedQty = 25,
                ReleaseReason = "PickCompleted",
                Timestamp = now
            },
            new QCPassedEvent
            {
                AggregateId = Guid.NewGuid(),
                UserId = "qc-1",
                TraceId = "trace-f",
                ItemId = 42,
                SKU = "RM-0001",
                Qty = 100,
                FromLocation = "QC_HOLD",
                ToLocation = "RECEIVING",
                Timestamp = now
            },
            new QCFailedEvent
            {
                AggregateId = Guid.NewGuid(),
                UserId = "qc-2",
                TraceId = "trace-g",
                ItemId = 42,
                SKU = "RM-0001",
                Qty = 10,
                FromLocation = "QC_HOLD",
                ToLocation = "QUARANTINE",
                ReasonCode = "DAMAGE",
                Timestamp = now
            },
            new InboundShipmentCreatedEvent
            {
                AggregateId = Guid.NewGuid(),
                UserId = "operator-3",
                TraceId = "trace-h",
                ShipmentId = Guid.NewGuid(),
                ReferenceNumber = "PO-100",
                SupplierId = 12,
                SupplierName = "Supplier",
                TotalLines = 2,
                TotalExpectedQty = 200,
                Timestamp = now
            }
        };

        foreach (var evt in events)
        {
            var json = JsonSerializer.Serialize(evt, evt.GetType(), JsonOptions);
            var clone = JsonSerializer.Deserialize(json, evt.GetType(), JsonOptions);
            clone.Should().NotBeNull();

            var cloneEvent = (WarehouseOperationalEvent)clone!;
            cloneEvent.AggregateId.Should().Be(evt.AggregateId);
            cloneEvent.UserId.Should().Be(evt.UserId);
            cloneEvent.EventId.Should().Be(evt.EventId);
        }
    }
}

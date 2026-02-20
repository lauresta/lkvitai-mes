using FluentAssertions;
using LKvitai.MES.Application.EventVersioning;
using LKvitai.MES.Contracts.Events;
using LKvitai.MES.Infrastructure.EventVersioning;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using ContractsDomainEvent = LKvitai.MES.Contracts.Events.DomainEvent;

namespace LKvitai.MES.Tests.Unit;

[Trait("Category", "EventVersioning")]
public sealed class EventSchemaVersionRegistryTests
{
    [Fact]
    public void DomainEvent_DefaultSchemaVersion_IsV1()
    {
        var evt = new StockMovedEvent();

        evt.SchemaVersion.Should().Be("v1");
    }

    [Fact]
    public void EnsureKnownVersion_ForKnownEventVersion_DoesNotThrow()
    {
        var registry = BuildRegistry(new StockMovedV1ToStockMovedEventUpcaster());

        var act = () => registry.EnsureKnownVersion(typeof(StockMovedEvent), "v1");

        act.Should().NotThrow();
    }

    [Fact]
    public void EnsureKnownVersion_ForUnknownVersion_Throws()
    {
        var registry = BuildRegistry(new StockMovedV1ToStockMovedEventUpcaster());

        var act = () => registry.EnsureKnownVersion(typeof(StockMovedEvent), "v99");

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Unknown event version*");
    }

    [Fact]
    public void UpcastToLatest_FromV1_AppliesSampleUpcaster()
    {
        var registry = BuildRegistry(new StockMovedV1ToStockMovedEventUpcaster());

        var v1 = new StockMovedV1Event
        {
            SchemaVersion = "v1",
            MovementId = Guid.NewGuid(),
            SKU = "SKU-001",
            Quantity = 12m,
            From = "A1",
            To = "B2",
            OperatorId = Guid.NewGuid(),
            Reason = "test"
        };

        var result = registry.UpcastToLatest(v1);

        result.Should().BeOfType<StockMovedEvent>();
        var v2 = (StockMovedEvent)result;
        v2.SchemaVersion.Should().Be("v2");
        v2.FromLocation.Should().Be("A1");
        v2.ToLocation.Should().Be("B2");
        v2.MovementType.Should().Be("TRANSFER");
    }

    [Fact]
    public void UpcastToLatest_AppliesUpcasterChain()
    {
        var registry = BuildRegistry(
            new StockMovedV1ToStockMovedEventUpcaster(),
            new StockMovedV2ToV3Upcaster());

        var v1 = new StockMovedV1Event
        {
            SchemaVersion = "v1",
            MovementId = Guid.NewGuid(),
            SKU = "SKU-XYZ",
            Quantity = 5m,
            From = "FROM-1",
            To = "TO-2",
            OperatorId = Guid.NewGuid()
        };

        var result = registry.UpcastToLatest(v1);

        result.Should().BeOfType<StockMovedV3Event>();
        ((StockMovedV3Event)result).SchemaVersion.Should().Be("v3");
    }

    private static EventSchemaVersionRegistry BuildRegistry(params IEventUpcaster[] upcasters)
    {
        return new EventSchemaVersionRegistry(
            upcasters,
            new Mock<ILogger<EventSchemaVersionRegistry>>().Object);
    }

    private sealed class StockMovedV3Event : ContractsDomainEvent
    {
        public Guid MovementId { get; set; }
        public string SKU { get; set; } = string.Empty;
        public string SourceLocation { get; set; } = string.Empty;
        public string DestinationLocation { get; set; } = string.Empty;
        public string MovementType { get; set; } = string.Empty;
    }

    private sealed class StockMovedV2ToV3Upcaster : EventUpcaster<StockMovedEvent, StockMovedV3Event>
    {
        public override string SourceVersion => "v2";
        public override string TargetVersion => "v3";

        public override StockMovedV3Event Upcast(StockMovedEvent source)
        {
            return new StockMovedV3Event
            {
                SchemaVersion = TargetVersion,
                EventId = source.EventId,
                Timestamp = source.Timestamp,
                Version = source.Version,
                MovementId = source.MovementId,
                SKU = source.SKU,
                SourceLocation = source.FromLocation,
                DestinationLocation = source.ToLocation,
                MovementType = source.MovementType
            };
        }
    }
}

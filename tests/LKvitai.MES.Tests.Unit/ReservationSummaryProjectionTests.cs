using FluentAssertions;
using LKvitai.MES.Contracts.Events;
using LKvitai.MES.Modules.Warehouse.Domain.Aggregates;
using LKvitai.MES.Modules.Warehouse.Projections;
using Xunit;

namespace LKvitai.MES.Tests.Unit;

public class ReservationSummaryProjectionTests
{
    [Fact]
    public void Create_ReservationCreatedEvent_SetsInitialFields()
    {
        var reservationId = Guid.NewGuid();
        var createdAt = DateTime.UtcNow;
        var evt = new ReservationCreatedEvent
        {
            ReservationId = reservationId,
            Purpose = "SO-123",
            Priority = 7,
            Timestamp = createdAt,
            RequestedLines = new List<ReservationLineDto>
            {
                new() { SKU = "SKU-1", Quantity = 2m },
                new() { SKU = "SKU-2", Quantity = 5m }
            }
        };

        var view = ReservationSummaryAggregation.Create(evt);

        view.ReservationId.Should().Be(reservationId);
        view.Purpose.Should().Be("SO-123");
        view.Priority.Should().Be(7);
        view.Status.Should().Be(ReservationStatus.PENDING.ToString());
        view.LockType.Should().Be(ReservationLockType.SOFT.ToString());
        view.CreatedAt.Should().Be(createdAt);
        view.PickingStartedAt.Should().BeNull();
        view.LineCount.Should().Be(2);
    }

    [Fact]
    public void Apply_StockAllocatedEvent_UpdatesStatusAndLockType()
    {
        var view = ReservationSummaryAggregation.Create(new ReservationCreatedEvent
        {
            ReservationId = Guid.NewGuid()
        });

        ReservationSummaryAggregation.ApplyAllocated(new StockAllocatedEvent(), view);

        view.Status.Should().Be(ReservationStatus.ALLOCATED.ToString());
        view.LockType.Should().Be(ReservationLockType.SOFT.ToString());
    }

    [Fact]
    public void Apply_PickingStartedEvent_UpdatesStatusLockAndTimestamp()
    {
        var pickingStartedAt = DateTime.UtcNow;
        var view = ReservationSummaryAggregation.Create(new ReservationCreatedEvent
        {
            ReservationId = Guid.NewGuid()
        });

        ReservationSummaryAggregation.ApplyPickingStarted(new PickingStartedEvent
        {
            Timestamp = pickingStartedAt
        }, view);

        view.Status.Should().Be(ReservationStatus.PICKING.ToString());
        view.LockType.Should().Be(ReservationLockType.HARD.ToString());
        view.PickingStartedAt.Should().Be(pickingStartedAt);
    }

    [Fact]
    public void Apply_ReservationConsumedEvent_UpdatesStatus()
    {
        var view = ReservationSummaryAggregation.Create(new ReservationCreatedEvent
        {
            ReservationId = Guid.NewGuid()
        });

        ReservationSummaryAggregation.ApplyConsumed(new ReservationConsumedEvent(), view);

        view.Status.Should().Be(ReservationStatus.CONSUMED.ToString());
    }

    [Fact]
    public void Apply_ReservationCancelledEvent_UpdatesStatus()
    {
        var view = ReservationSummaryAggregation.Create(new ReservationCreatedEvent
        {
            ReservationId = Guid.NewGuid()
        });

        ReservationSummaryAggregation.ApplyCancelled(new ReservationCancelledEvent(), view);

        view.Status.Should().Be(ReservationStatus.CANCELLED.ToString());
    }

    [Fact]
    public void Apply_ReservationBumpedEvent_UpdatesStatusWhenReservationIsBumped()
    {
        var reservationId = Guid.NewGuid();
        var view = ReservationSummaryAggregation.Create(new ReservationCreatedEvent
        {
            ReservationId = reservationId
        });

        ReservationSummaryAggregation.ApplyBumped(new ReservationBumpedEvent
        {
            BumpedReservationId = reservationId,
            BumpingReservationId = Guid.NewGuid()
        }, view);

        view.Status.Should().Be(ReservationStatus.BUMPED.ToString());
    }
}

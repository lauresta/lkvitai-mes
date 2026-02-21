using FluentAssertions;
using LKvitai.MES.Contracts.Events;
using LKvitai.MES.Contracts.ReadModels;
using LKvitai.MES.Modules.Warehouse.Projections;
using Xunit;

namespace LKvitai.MES.Tests.Warehouse.Unit;

/// <summary>
/// Unit tests for AvailableStock projection logic (Package D).
///
/// Verifies:
///   - StockMoved increases/decreases onHandQty → impacts availableQty
///   - PickingStarted increases hardLockedQty → decreases availableQty
///   - ReservationConsumed removes hardLockedQty → restores availableQty
///   - ReservationCancelled removes hardLockedQty → restores availableQty
///   - availableQty is never negative (clamped to 0)
///   - Deterministic: uses only self-contained event data (V-5 Rule B)
/// </summary>
public class AvailableStockProjectionTests
{
    private const string WarehouseId = "MAIN";
    private const string Location = "BIN-001";
    private const string Sku = "SKU933";
    private static readonly string StreamId = $"stock-ledger:{WarehouseId}:{Location}:{Sku}";
    private static readonly string ViewId = AvailableStockView.ComputeId(WarehouseId, Location, Sku);

    // ── StockMoved → onHand impacts available ─────────────────────────

    [Fact]
    public void Apply_StockMovedReceipt_IncreasesOnHandAndAvailable()
    {
        // Arrange: receipt from SUPPLIER into BIN-001
        var evt = MakeStockMoved("SUPPLIER", Location, 100m);
        var view = MakeView(onHand: 0m, hardLocked: 0m);

        // Act
        var result = AvailableStockAggregation.Apply(evt, view, StreamId);

        // Assert
        result.OnHandQty.Should().Be(100m);
        result.HardLockedQty.Should().Be(0m);
        result.AvailableQty.Should().Be(100m);
    }

    [Fact]
    public void Apply_StockMovedPick_DecreasesOnHandAndAvailable()
    {
        // Arrange: pick from BIN-001 to PRODUCTION
        var evt = MakeStockMoved(Location, "PRODUCTION", 30m);
        var view = MakeView(onHand: 100m, hardLocked: 0m);

        // Act
        var result = AvailableStockAggregation.Apply(evt, view, StreamId);

        // Assert
        result.OnHandQty.Should().Be(70m);
        result.AvailableQty.Should().Be(70m);
    }

    [Fact]
    public void Apply_StockMovedTransfer_FromDecreasesToIncreases()
    {
        // Arrange: transfer from BIN-001 to BIN-002
        var evt = MakeStockMoved(Location, "BIN-002", 50m);

        var fromView = MakeView(onHand: 100m, hardLocked: 0m);
        var toView = new AvailableStockView
        {
            Id = AvailableStockView.ComputeId(WarehouseId, "BIN-002", Sku),
            WarehouseId = WarehouseId,
            Location = "BIN-002",
            SKU = Sku,
            OnHandQty = 0m,
            HardLockedQty = 0m,
            AvailableQty = 0m,
            LastUpdated = DateTime.UtcNow.AddHours(-1)
        };

        // Act
        var fromResult = AvailableStockAggregation.Apply(evt, fromView, StreamId);
        var toResult = AvailableStockAggregation.Apply(evt, toView, StreamId);

        // Assert
        fromResult.OnHandQty.Should().Be(50m, "FROM location should decrease");
        fromResult.AvailableQty.Should().Be(50m);

        toResult.OnHandQty.Should().Be(50m, "TO location should increase");
        toResult.AvailableQty.Should().Be(50m);
    }

    // ── PickingStarted → hardLocked increases, available decreases ────

    [Fact]
    public void ApplyPickingStarted_IncreasesHardLockedQty_DecreasesAvailable()
    {
        // Arrange
        var evt = MakePickingStarted(40m);
        var view = MakeView(onHand: 100m, hardLocked: 0m);

        // Act
        var result = AvailableStockAggregation.ApplyPickingStarted(evt, view);

        // Assert
        result.OnHandQty.Should().Be(100m, "onHand unchanged by picking start");
        result.HardLockedQty.Should().Be(40m);
        result.AvailableQty.Should().Be(60m, "available = 100 - 40");
    }

    [Fact]
    public void ApplyPickingStarted_MultipleLocks_AccumulatesHardLockedQty()
    {
        // Arrange: two sequential picking starts
        var evt1 = MakePickingStarted(20m);
        var evt2 = MakePickingStarted(30m);
        var view = MakeView(onHand: 100m, hardLocked: 0m);

        // Act
        var after1 = AvailableStockAggregation.ApplyPickingStarted(evt1, view);
        var after2 = AvailableStockAggregation.ApplyPickingStarted(evt2, after1);

        // Assert
        after2.HardLockedQty.Should().Be(50m);
        after2.AvailableQty.Should().Be(50m, "available = 100 - 50");
    }

    // ── ReservationConsumed → hardLocked decreases, available restores ─

    [Fact]
    public void ApplyReservationConsumed_DecreasesHardLockedQty_RestoresAvailable()
    {
        // Arrange: 40 units hard-locked, then reservation consumed
        var consumed = MakeReservationConsumed(40m);
        var view = MakeView(onHand: 100m, hardLocked: 40m);

        // Act
        var result = AvailableStockAggregation.ApplyReservationConsumed(consumed, view);

        // Assert
        result.HardLockedQty.Should().Be(0m);
        result.AvailableQty.Should().Be(100m, "available restored after consumption");
    }

    // ── ReservationCancelled → hardLocked decreases, available restores ─

    [Fact]
    public void ApplyReservationCancelled_DecreasesHardLockedQty_RestoresAvailable()
    {
        // Arrange: 25 units hard-locked, then reservation cancelled
        var cancelled = MakeReservationCancelled(25m);
        var view = MakeView(onHand: 80m, hardLocked: 25m);

        // Act
        var result = AvailableStockAggregation.ApplyReservationCancelled(cancelled, view);

        // Assert
        result.HardLockedQty.Should().Be(0m);
        result.AvailableQty.Should().Be(80m, "available restored after cancellation");
    }

    // ── Non-negative available qty guarantee ──────────────────────────

    [Fact]
    public void Available_NeverNegative_WhenHardLockedExceedsOnHand()
    {
        // Edge case: hardLockedQty could temporarily exceed onHandQty
        // during async projection lag (e.g., pick movement processed before
        // lock release). AvailableQty must clamp to 0.
        var view = MakeView(onHand: 10m, hardLocked: 30m);
        view.RecomputeAvailable();

        view.AvailableQty.Should().Be(0m, "availableQty must never go negative");
    }

    [Fact]
    public void Available_NeverNegative_AfterOverdraw()
    {
        // Simulate an overdraw scenario via events
        var receipt = MakeStockMoved("SUPPLIER", Location, 20m);
        var pick = MakeStockMoved(Location, "PRODUCTION", 25m);

        var view = MakeView(onHand: 0m, hardLocked: 0m);
        view = AvailableStockAggregation.Apply(receipt, view, StreamId);
        view = AvailableStockAggregation.Apply(pick, view, StreamId);

        // onHand = 20 - 25 = -5 (can happen with virtual location routing)
        // available must be clamped to 0
        view.AvailableQty.Should().Be(0m);
    }

    // ── Full lifecycle scenario ───────────────────────────────────────

    [Fact]
    public void FullLifecycle_Receipt_Lock_Pick_Consume()
    {
        var view = MakeView(onHand: 0m, hardLocked: 0m);

        // 1. Receipt: 100 units arrive
        var receipt = MakeStockMoved("SUPPLIER", Location, 100m);
        view = AvailableStockAggregation.Apply(receipt, view, StreamId);
        view.OnHandQty.Should().Be(100m);
        view.AvailableQty.Should().Be(100m);

        // 2. StartPicking: 40 units hard-locked
        var picking = MakePickingStarted(40m);
        view = AvailableStockAggregation.ApplyPickingStarted(picking, view);
        view.OnHandQty.Should().Be(100m);
        view.HardLockedQty.Should().Be(40m);
        view.AvailableQty.Should().Be(60m);

        // 3. Pick: 40 units moved to PRODUCTION (onHand decreases)
        var pickMove = MakeStockMoved(Location, "PRODUCTION", 40m);
        view = AvailableStockAggregation.Apply(pickMove, view, StreamId);
        view.OnHandQty.Should().Be(60m);
        view.HardLockedQty.Should().Be(40m);
        view.AvailableQty.Should().Be(20m);

        // 4. ReservationConsumed: hard lock released
        var consumed = MakeReservationConsumed(40m);
        view = AvailableStockAggregation.ApplyReservationConsumed(consumed, view);
        view.OnHandQty.Should().Be(60m);
        view.HardLockedQty.Should().Be(0m);
        view.AvailableQty.Should().Be(60m);
    }

    // ── V-5 Rule B: self-contained event data ─────────────────────────

    [Fact]
    public void Apply_UsesOnlyEventData_NoExternalQueries()
    {
        // This test validates V-5 Rule B: Self-contained event data.
        // All Apply methods should work with ONLY the event + current view.
        var evt = MakeStockMoved("SUPPLIER", Location, 50m);
        var view = MakeView(onHand: 0m, hardLocked: 0m);

        // Act - should not throw or require external data
        AvailableStockView? result = null;
        var act = () => result = AvailableStockAggregation.Apply(evt, view, StreamId);

        // Assert - completes without external dependencies
        act.Should().NotThrow();
        result!.OnHandQty.Should().Be(50m);
        result.AvailableQty.Should().Be(50m);
    }

    // ── ComputeId tests ───────────────────────────────────────────────

    [Fact]
    public void ComputeId_ShouldReturnDeterministicCompositeKey()
    {
        var id = AvailableStockView.ComputeId("WH1", "LOC-A", "SKU-001");
        id.Should().Be("WH1:LOC-A:SKU-001");
    }

    [Fact]
    public void ComputeId_SameInputs_ShouldReturnSameId()
    {
        var id1 = AvailableStockView.ComputeId("WH1", "LOC-A", "SKU-001");
        var id2 = AvailableStockView.ComputeId("WH1", "LOC-A", "SKU-001");
        id1.Should().Be(id2);
    }

    [Fact]
    public void ComputeId_DifferentInputs_ShouldReturnDifferentIds()
    {
        var id1 = AvailableStockView.ComputeId("WH1", "LOC-A", "SKU-001");
        var id2 = AvailableStockView.ComputeId("WH1", "LOC-A", "SKU-002");
        var id3 = AvailableStockView.ComputeId("WH1", "LOC-B", "SKU-001");
        id1.Should().NotBe(id2);
        id1.Should().NotBe(id3);
    }

    // ── Empty ReleasedHardLockLines ───────────────────────────────────

    [Fact]
    public void ApplyReservationConsumed_EmptyReleasedLines_NoChange()
    {
        var consumed = new ReservationConsumedEvent
        {
            ReservationId = Guid.NewGuid(),
            ActualQuantity = 50m,
            ReleasedHardLockLines = new List<HardLockLineDto>() // empty
        };
        var view = MakeView(onHand: 100m, hardLocked: 30m);

        var result = AvailableStockAggregation.ApplyReservationConsumed(consumed, view);

        result.HardLockedQty.Should().Be(30m, "no matching line → no change");
        result.AvailableQty.Should().Be(70m);
    }

    [Fact]
    public void ApplyPickingStarted_EmptyLines_NoChange()
    {
        var picking = new PickingStartedEvent
        {
            ReservationId = Guid.NewGuid(),
            LockType = "HARD",
            HardLockedLines = new List<HardLockLineDto>()
        };
        var view = MakeView(onHand: 100m, hardLocked: 0m);

        var result = AvailableStockAggregation.ApplyPickingStarted(picking, view);

        result.HardLockedQty.Should().Be(0m);
        result.AvailableQty.Should().Be(100m);
    }

    // ── Helpers ───────────────────────────────────────────────────────

    private static AvailableStockView MakeView(decimal onHand, decimal hardLocked)
    {
        var view = new AvailableStockView
        {
            Id = ViewId,
            WarehouseId = WarehouseId,
            Location = Location,
            SKU = Sku,
            OnHandQty = onHand,
            HardLockedQty = hardLocked,
            LastUpdated = DateTime.UtcNow.AddHours(-1)
        };
        view.RecomputeAvailable();
        return view;
    }

    private static StockMovedEvent MakeStockMoved(string from, string to, decimal qty) => new()
    {
        MovementId = Guid.NewGuid(),
        SKU = Sku,
        Quantity = qty,
        FromLocation = from,
        ToLocation = to,
        MovementType = "RECEIPT",
        OperatorId = Guid.NewGuid(),
        Timestamp = DateTime.UtcNow
    };

    private static PickingStartedEvent MakePickingStarted(decimal qty) => new()
    {
        ReservationId = Guid.NewGuid(),
        LockType = "HARD",
        Timestamp = DateTime.UtcNow,
        HardLockedLines = new List<HardLockLineDto>
        {
            new()
            {
                WarehouseId = WarehouseId,
                Location = Location,
                SKU = Sku,
                HardLockedQty = qty
            }
        }
    };

    private static ReservationConsumedEvent MakeReservationConsumed(decimal qty) => new()
    {
        ReservationId = Guid.NewGuid(),
        ActualQuantity = qty,
        Timestamp = DateTime.UtcNow,
        ReleasedHardLockLines = new List<HardLockLineDto>
        {
            new()
            {
                WarehouseId = WarehouseId,
                Location = Location,
                SKU = Sku,
                HardLockedQty = qty
            }
        }
    };

    private static ReservationCancelledEvent MakeReservationCancelled(decimal qty) => new()
    {
        ReservationId = Guid.NewGuid(),
        Reason = "Test cancellation",
        Timestamp = DateTime.UtcNow,
        ReleasedHardLockLines = new List<HardLockLineDto>
        {
            new()
            {
                WarehouseId = WarehouseId,
                Location = Location,
                SKU = Sku,
                HardLockedQty = qty
            }
        }
    };
}

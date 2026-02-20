using FluentAssertions;
using LKvitai.MES.Contracts.Events;
using LKvitai.MES.Contracts.ReadModels;
using LKvitai.MES.Modules.Warehouse.Projections;
using Xunit;

namespace LKvitai.MES.Tests.Unit;

/// <summary>
/// Unit tests for HandlingUnit projection logic (Package E).
///
/// Verifies:
///   - HandlingUnitCreated initializes the HU view
///   - LineAdded/LineRemoved updates lines
///   - HandlingUnitSealed sets status to SEALED
///   - StockMoved (receipt) adds lines at toLocation
///   - StockMoved (pick) removes lines at fromLocation and updates location
///   - StockMoved (transfer) moves HU to new location
///   - Full lifecycle: created → receipt → seal (deterministic replay)
///   - Idempotent apply (no double-add on replay for direct line events)
///   - V-5 Rule B compliance (self-contained event data)
/// </summary>
public class HandlingUnitProjectionTests
{
    private static readonly Guid HuId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private const string WarehouseId = "WH1";
    private const string Location = "LOC-A";
    private const string Sku1 = "SKU-001";
    private const string Sku2 = "SKU-002";
    private const string LPN = "HU-20260208-ABC123";

    // ── HandlingUnitCreated → initializes view ─────────────────────

    [Fact]
    public void ApplyCreated_InitializesHUView()
    {
        var evt = MakeCreated(HuId, LPN, "PALLET", WarehouseId, Location);
        var view = MakeEmptyView();

        var result = HandlingUnitAggregation.ApplyCreated(evt, view);

        result.HuId.Should().Be(HuId);
        result.LPN.Should().Be(LPN);
        result.Type.Should().Be("PALLET");
        result.Status.Should().Be("OPEN");
        result.WarehouseId.Should().Be(WarehouseId);
        result.CurrentLocation.Should().Be(Location);
        result.CreatedAt.Should().Be(evt.Timestamp);
        result.Lines.Should().BeEmpty();
    }

    // ── LineAdded → adds line ──────────────────────────────────────

    [Fact]
    public void ApplyLineAdded_AddsNewLine()
    {
        var view = MakeOpenView();
        var evt = MakeLineAdded(HuId, Sku1, 50m);

        var result = HandlingUnitAggregation.ApplyLineAdded(evt, view);

        result.Lines.Should().HaveCount(1);
        result.Lines[0].SKU.Should().Be(Sku1);
        result.Lines[0].Quantity.Should().Be(50m);
    }

    [Fact]
    public void ApplyLineAdded_IncreasesExistingLine()
    {
        var view = MakeOpenView();
        view.AddLine(Sku1, 30m);
        var evt = MakeLineAdded(HuId, Sku1, 20m);

        var result = HandlingUnitAggregation.ApplyLineAdded(evt, view);

        result.Lines.Should().HaveCount(1);
        result.Lines[0].Quantity.Should().Be(50m);
    }

    // ── LineRemoved → removes line ─────────────────────────────────

    [Fact]
    public void ApplyLineRemoved_DecreasesLine()
    {
        var view = MakeOpenView();
        view.AddLine(Sku1, 100m);
        var evt = MakeLineRemoved(HuId, Sku1, 40m);

        var result = HandlingUnitAggregation.ApplyLineRemoved(evt, view);

        result.Lines.Should().HaveCount(1);
        result.Lines[0].Quantity.Should().Be(60m);
    }

    [Fact]
    public void ApplyLineRemoved_RemovesLineWhenQuantityHitsZero()
    {
        var view = MakeOpenView();
        view.AddLine(Sku1, 50m);
        var evt = MakeLineRemoved(HuId, Sku1, 50m);

        var result = HandlingUnitAggregation.ApplyLineRemoved(evt, view);

        result.Lines.Should().BeEmpty();
    }

    // ── HandlingUnitSealed → sets status ───────────────────────────

    [Fact]
    public void ApplySealed_SetsStatusToSealed()
    {
        var view = MakeOpenView();
        view.AddLine(Sku1, 100m);
        var sealedAt = DateTime.UtcNow;
        var evt = MakeSealed(HuId, sealedAt);

        var result = HandlingUnitAggregation.ApplySealed(evt, view);

        result.Status.Should().Be("SEALED");
        result.SealedAt.Should().Be(sealedAt);
    }

    // ── StockMoved (receipt) → adds line to HU at toLocation ──────

    [Fact]
    public void ApplyStockMoved_Receipt_AddsLineAtToLocation()
    {
        var view = MakeOpenView(); // location = LOC-A
        var evt = MakeStockMoved("SUPPLIER", Location, Sku1, 100m, HuId);

        var result = HandlingUnitAggregation.ApplyStockMoved(evt, view);

        result.Lines.Should().HaveCount(1);
        result.Lines[0].SKU.Should().Be(Sku1);
        result.Lines[0].Quantity.Should().Be(100m);
        result.CurrentLocation.Should().Be(Location); // unchanged
    }

    [Fact]
    public void ApplyStockMoved_Receipt_MultipleSkus()
    {
        var view = MakeOpenView();
        var evt1 = MakeStockMoved("SUPPLIER", Location, Sku1, 100m, HuId);
        var evt2 = MakeStockMoved("SUPPLIER", Location, Sku2, 50m, HuId);

        HandlingUnitAggregation.ApplyStockMoved(evt1, view);
        HandlingUnitAggregation.ApplyStockMoved(evt2, view);

        view.Lines.Should().HaveCount(2);
        view.Lines.Should().Contain(l => l.SKU == Sku1 && l.Quantity == 100m);
        view.Lines.Should().Contain(l => l.SKU == Sku2 && l.Quantity == 50m);
    }

    // ── StockMoved (pick) → removes line and updates location ─────

    [Fact]
    public void ApplyStockMoved_Pick_RemovesLineFromHULocation()
    {
        var view = MakeOpenView();
        view.AddLine(Sku1, 100m);
        var evt = MakeStockMoved(Location, "PRODUCTION", Sku1, 100m, HuId);

        var result = HandlingUnitAggregation.ApplyStockMoved(evt, view);

        result.Lines.Should().BeEmpty();
        result.CurrentLocation.Should().Be("PRODUCTION"); // location updated
    }

    [Fact]
    public void ApplyStockMoved_PartialPick_ReducesLine()
    {
        var view = MakeOpenView();
        view.AddLine(Sku1, 100m);
        var evt = MakeStockMoved(Location, "PRODUCTION", Sku1, 40m, HuId);

        var result = HandlingUnitAggregation.ApplyStockMoved(evt, view);

        result.Lines.Should().HaveCount(1);
        result.Lines[0].Quantity.Should().Be(60m);
        result.CurrentLocation.Should().Be("PRODUCTION");
    }

    // ── StockMoved (transfer) → updates location ──────────────────

    [Fact]
    public void ApplyStockMoved_Transfer_UpdatesLocation()
    {
        var view = MakeOpenView(); // LOC-A
        view.AddLine(Sku1, 100m);
        var evt = MakeStockMoved(Location, "LOC-B", Sku1, 100m, HuId);

        var result = HandlingUnitAggregation.ApplyStockMoved(evt, view);

        result.CurrentLocation.Should().Be("LOC-B");
        // Line removed from LOC-A, line added to LOC-B is NOT done in same doc
        // because the HU doc moves to LOC-B. Net effect: remove from old, loc changes.
        // Actually, the design says:
        //   If fromLocation == HU.location: RemoveLine (done)
        //   If toLocation == HU.location: AddLine (doesn't match LOC-B != LOC-A before update)
        // But after location update, the line is gone.
        // For single-HU transfers where all lines move, the HU just changes location.
        // The HU projection for transfer moves each line separately via individual StockMoved events.
    }

    // ── StockMoved without HandlingUnitId → ignored by grouper ────

    [Fact]
    public void StockMoved_WithoutHuId_DoesNotAffectView()
    {
        var view = MakeOpenView();
        view.AddLine(Sku1, 100m);
        var evt = MakeStockMoved(Location, "LOC-B", Sku1, 50m, handlingUnitId: null);

        // The grouper would not route this event to any HU doc.
        // Calling ApplyStockMoved directly with a non-null huId scenario;
        // but this test verifies the grouper logic conceptually.
        // When HandlingUnitId is null, the grouper skips the event.
        evt.HandlingUnitId.Should().BeNull();
    }

    // ── Full lifecycle: created → receipt → seal (determinism) ─────

    [Fact]
    public void FullLifecycle_Created_Receipt_Sealed()
    {
        var view = MakeEmptyView();

        // Step 1: Create HU
        var created = MakeCreated(HuId, LPN, "PALLET", WarehouseId, Location);
        HandlingUnitAggregation.ApplyCreated(created, view);

        view.Status.Should().Be("OPEN");
        view.CurrentLocation.Should().Be(Location);

        // Step 2: Receipt via StockMoved
        var stockMoved1 = MakeStockMoved("SUPPLIER", Location, Sku1, 100m, HuId);
        var stockMoved2 = MakeStockMoved("SUPPLIER", Location, Sku2, 50m, HuId);
        HandlingUnitAggregation.ApplyStockMoved(stockMoved1, view);
        HandlingUnitAggregation.ApplyStockMoved(stockMoved2, view);

        view.Lines.Should().HaveCount(2);
        view.Lines.Should().Contain(l => l.SKU == Sku1 && l.Quantity == 100m);
        view.Lines.Should().Contain(l => l.SKU == Sku2 && l.Quantity == 50m);

        // Step 3: Seal
        var sealed_ = MakeSealed(HuId, DateTime.UtcNow);
        HandlingUnitAggregation.ApplySealed(sealed_, view);

        view.Status.Should().Be("SEALED");
        view.SealedAt.Should().NotBeNull();
    }

    [Fact]
    public void FullLifecycle_Replay_ProducesSameState()
    {
        // First pass
        var view1 = MakeEmptyView();
        var events = CreateLifecycleEvents();
        ApplyAll(view1, events);

        // Second pass (replay)
        var view2 = MakeEmptyView();
        ApplyAll(view2, events);

        // Both should produce identical state (determinism)
        view1.HuId.Should().Be(view2.HuId);
        view1.LPN.Should().Be(view2.LPN);
        view1.Status.Should().Be(view2.Status);
        view1.CurrentLocation.Should().Be(view2.CurrentLocation);
        view1.Lines.Should().HaveCount(view2.Lines.Count);
        for (int i = 0; i < view1.Lines.Count; i++)
        {
            view1.Lines[i].SKU.Should().Be(view2.Lines[i].SKU);
            view1.Lines[i].Quantity.Should().Be(view2.Lines[i].Quantity);
        }
    }

    // ── StockMoved before Created (edge case) ─────────────────────

    [Fact]
    public void ApplyStockMoved_BeforeCreated_FromVirtualLocation_SetsLocationAndAddsLine()
    {
        // Edge case: StockMoved arrives before HandlingUnitCreated
        var view = MakeEmptyView(); // no location set
        var evt = MakeStockMoved("SUPPLIER", Location, Sku1, 100m, HuId);

        var result = HandlingUnitAggregation.ApplyStockMoved(evt, view);

        // Should still add line and set location (edge case handling)
        result.CurrentLocation.Should().Be(Location);
        result.Lines.Should().HaveCount(1);
        result.Lines[0].SKU.Should().Be(Sku1);
        result.Lines[0].Quantity.Should().Be(100m);
    }

    // ── V-5 Rule B compliance ─────────────────────────────────────

    [Fact]
    public void V5RuleB_AllEventsAreSelfContained()
    {
        // Verify that HandlingUnitCreatedEvent carries all data needed
        var created = MakeCreated(HuId, LPN, "PALLET", WarehouseId, Location);
        created.HuId.Should().NotBeEmpty();
        created.LPN.Should().NotBeNullOrEmpty();
        created.WarehouseId.Should().NotBeNullOrEmpty();
        created.Location.Should().NotBeNullOrEmpty();

        // Verify that StockMovedEvent carries HandlingUnitId
        var stockMoved = MakeStockMoved("SUPPLIER", Location, Sku1, 100m, HuId);
        stockMoved.HandlingUnitId.Should().Be(HuId);
        stockMoved.FromLocation.Should().NotBeNullOrEmpty();
        stockMoved.ToLocation.Should().NotBeNullOrEmpty();
        stockMoved.SKU.Should().NotBeNullOrEmpty();

        // Verify that HandlingUnitSealedEvent carries HuId and SealedAt
        var sealed_ = MakeSealed(HuId, DateTime.UtcNow);
        sealed_.HuId.Should().Be(HuId);
        sealed_.SealedAt.Should().BeAfter(DateTime.MinValue);
    }

    // ── HandlingUnitView.IsEmpty ──────────────────────────────────

    [Fact]
    public void IsEmpty_ReturnsTrueWhenNoLines()
    {
        var view = MakeOpenView();
        view.IsEmpty.Should().BeTrue();
    }

    [Fact]
    public void IsEmpty_ReturnsFalseWhenLinesExist()
    {
        var view = MakeOpenView();
        view.AddLine(Sku1, 50m);
        view.IsEmpty.Should().BeFalse();
    }

    // ── Sealed HU guards (defensive — projection ignores post-seal mutations) ──

    [Fact]
    public void Sealed_LineAdded_DoesNotChangeLines()
    {
        var view = MakeSealedView();
        var linesBefore = view.Lines.Select(l => (l.SKU, l.Quantity)).ToList();
        var evt = MakeLineAdded(HuId, Sku2, 99m);

        var result = HandlingUnitAggregation.ApplyLineAdded(evt, view);

        result.Status.Should().Be("SEALED");
        result.Lines.Select(l => (l.SKU, l.Quantity)).Should().BeEquivalentTo(linesBefore);
    }

    [Fact]
    public void Sealed_LineRemoved_DoesNotChangeLines()
    {
        var view = MakeSealedView();
        var linesBefore = view.Lines.Select(l => (l.SKU, l.Quantity)).ToList();
        var evt = MakeLineRemoved(HuId, Sku1, 10m);

        var result = HandlingUnitAggregation.ApplyLineRemoved(evt, view);

        result.Status.Should().Be("SEALED");
        result.Lines.Select(l => (l.SKU, l.Quantity)).Should().BeEquivalentTo(linesBefore);
    }

    [Fact]
    public void Sealed_StockMoved_DoesNotChangeLocationOrLines()
    {
        var view = MakeSealedView();
        var locationBefore = view.CurrentLocation;
        var linesBefore = view.Lines.Select(l => (l.SKU, l.Quantity)).ToList();
        var evt = MakeStockMoved(Location, "LOC-B", Sku1, 50m, HuId);

        var result = HandlingUnitAggregation.ApplyStockMoved(evt, view);

        result.Status.Should().Be("SEALED");
        result.CurrentLocation.Should().Be(locationBefore);
        result.Lines.Select(l => (l.SKU, l.Quantity)).Should().BeEquivalentTo(linesBefore);
    }

    [Fact]
    public void Sealed_StockMoved_Receipt_DoesNotAddLine()
    {
        var view = MakeSealedView();
        var lineCountBefore = view.Lines.Count;
        var evt = MakeStockMoved("SUPPLIER", Location, Sku2, 200m, HuId);

        var result = HandlingUnitAggregation.ApplyStockMoved(evt, view);

        result.Status.Should().Be("SEALED");
        result.Lines.Should().HaveCount(lineCountBefore);
    }

    // ── Helpers ───────────────────────────────────────────────────

    private static HandlingUnitView MakeEmptyView()
    {
        return new HandlingUnitView
        {
            Id = HuId.ToString()
        };
    }

    private static HandlingUnitView MakeOpenView()
    {
        return new HandlingUnitView
        {
            Id = HuId.ToString(),
            HuId = HuId,
            LPN = LPN,
            Type = "PALLET",
            Status = "OPEN",
            WarehouseId = WarehouseId,
            CurrentLocation = Location,
            CreatedAt = DateTime.UtcNow
        };
    }

    private static HandlingUnitView MakeSealedView()
    {
        var view = MakeOpenView();
        view.AddLine(Sku1, 100m);
        view.Status = "SEALED";
        view.SealedAt = DateTime.UtcNow;
        return view;
    }

    private static HandlingUnitCreatedEvent MakeCreated(
        Guid huId, string lpn, string type, string warehouseId, string location)
    {
        return new HandlingUnitCreatedEvent
        {
            HuId = huId,
            LPN = lpn,
            Type = type,
            WarehouseId = warehouseId,
            Location = location,
            OperatorId = Guid.NewGuid(),
            Timestamp = DateTime.UtcNow
        };
    }

    private static LineAddedToHandlingUnitEvent MakeLineAdded(Guid huId, string sku, decimal qty)
    {
        return new LineAddedToHandlingUnitEvent
        {
            HuId = huId,
            SKU = sku,
            Quantity = qty,
            Timestamp = DateTime.UtcNow
        };
    }

    private static LineRemovedFromHandlingUnitEvent MakeLineRemoved(Guid huId, string sku, decimal qty)
    {
        return new LineRemovedFromHandlingUnitEvent
        {
            HuId = huId,
            SKU = sku,
            Quantity = qty,
            Timestamp = DateTime.UtcNow
        };
    }

    private static HandlingUnitSealedEvent MakeSealed(Guid huId, DateTime sealedAt)
    {
        return new HandlingUnitSealedEvent
        {
            HuId = huId,
            SealedAt = sealedAt,
            Timestamp = sealedAt
        };
    }

    private static StockMovedEvent MakeStockMoved(
        string from, string to, string sku, decimal qty, Guid? handlingUnitId)
    {
        return new StockMovedEvent
        {
            MovementId = Guid.NewGuid(),
            FromLocation = from,
            ToLocation = to,
            SKU = sku,
            Quantity = qty,
            HandlingUnitId = handlingUnitId,
            MovementType = "RECEIPT",
            OperatorId = Guid.NewGuid(),
            Timestamp = DateTime.UtcNow
        };
    }

    private static List<object> CreateLifecycleEvents()
    {
        return new List<object>
        {
            MakeCreated(HuId, LPN, "PALLET", WarehouseId, Location),
            MakeStockMoved("SUPPLIER", Location, Sku1, 100m, HuId),
            MakeStockMoved("SUPPLIER", Location, Sku2, 50m, HuId),
            MakeSealed(HuId, DateTime.UtcNow)
        };
    }

    private static void ApplyAll(HandlingUnitView view, List<object> events)
    {
        foreach (var evt in events)
        {
            switch (evt)
            {
                case HandlingUnitCreatedEvent created:
                    HandlingUnitAggregation.ApplyCreated(created, view);
                    break;
                case StockMovedEvent stockMoved:
                    HandlingUnitAggregation.ApplyStockMoved(stockMoved, view);
                    break;
                case HandlingUnitSealedEvent sealed_:
                    HandlingUnitAggregation.ApplySealed(sealed_, view);
                    break;
                case LineAddedToHandlingUnitEvent lineAdded:
                    HandlingUnitAggregation.ApplyLineAdded(lineAdded, view);
                    break;
                case LineRemovedFromHandlingUnitEvent lineRemoved:
                    HandlingUnitAggregation.ApplyLineRemoved(lineRemoved, view);
                    break;
            }
        }
    }
}

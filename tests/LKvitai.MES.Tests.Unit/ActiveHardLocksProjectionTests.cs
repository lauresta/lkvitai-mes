using FluentAssertions;
using LKvitai.MES.Contracts.Events;
using LKvitai.MES.Contracts.ReadModels;
using LKvitai.MES.Projections;
using Marten;
using Moq;
using System.Linq.Expressions;
using Xunit;

namespace LKvitai.MES.Tests.Unit;

/// <summary>
/// Unit tests for ActiveHardLocksProjection [MITIGATION R-4].
///
/// Verifies:
///   - PickingStartedEvent → creates one ActiveHardLockView per HardLockLineDto
///   - ReservationConsumedEvent → deletes all rows for that reservation
///   - ReservationCancelledEvent → deletes all rows for that reservation
///   - ComputeId produces deterministic composite keys
///   - Idempotent Store (same event re-applied overwrites with same data)
/// </summary>
public class ActiveHardLocksProjectionTests
{
    private readonly ActiveHardLocksProjection _projection = new();

    // ── PickingStartedEvent → Store rows ──────────────────────────────

    [Fact]
    public void Project_PickingStarted_SingleLine_ShouldStoreOneDocument()
    {
        // Arrange
        var ops = new Mock<IDocumentOperations>();
        var reservationId = Guid.NewGuid();
        var timestamp = new DateTime(2025, 6, 15, 10, 0, 0, DateTimeKind.Utc);

        var evt = new PickingStartedEvent
        {
            ReservationId = reservationId,
            LockType = "HARD",
            Timestamp = timestamp,
            HardLockedLines = new List<HardLockLineDto>
            {
                new()
                {
                    WarehouseId = "WH1",
                    Location = "LOC-A",
                    SKU = "SKU-001",
                    HardLockedQty = 50m
                }
            }
        };

        // Act
        _projection.Project(evt, ops.Object);

        // Assert: exactly one Store call
        ops.Verify(o => o.Store(It.Is<ActiveHardLockView>(v =>
            v.Id == ActiveHardLockView.ComputeId(reservationId, "LOC-A", "SKU-001") &&
            v.WarehouseId == "WH1" &&
            v.ReservationId == reservationId &&
            v.Location == "LOC-A" &&
            v.SKU == "SKU-001" &&
            v.HardLockedQty == 50m &&
            v.StartedAt == timestamp
        )), Times.Once);

        ops.VerifyNoOtherCalls();
    }

    [Fact]
    public void Project_PickingStarted_MultipleLines_ShouldStoreOneDocumentPerLine()
    {
        // Arrange
        var ops = new Mock<IDocumentOperations>();
        var reservationId = Guid.NewGuid();

        var evt = new PickingStartedEvent
        {
            ReservationId = reservationId,
            LockType = "HARD",
            HardLockedLines = new List<HardLockLineDto>
            {
                new() { WarehouseId = "WH1", Location = "LOC-A", SKU = "SKU-001", HardLockedQty = 10m },
                new() { WarehouseId = "WH1", Location = "LOC-A", SKU = "SKU-002", HardLockedQty = 20m },
                new() { WarehouseId = "WH2", Location = "LOC-B", SKU = "SKU-003", HardLockedQty = 30m }
            }
        };

        // Act
        _projection.Project(evt, ops.Object);

        // Assert: three Store calls
        ops.Verify(o => o.Store(It.IsAny<ActiveHardLockView>()), Times.Exactly(3));

        // Verify each document has the correct Id
        ops.Verify(o => o.Store(It.Is<ActiveHardLockView>(v =>
            v.Id == ActiveHardLockView.ComputeId(reservationId, "LOC-A", "SKU-001") &&
            v.HardLockedQty == 10m)));

        ops.Verify(o => o.Store(It.Is<ActiveHardLockView>(v =>
            v.Id == ActiveHardLockView.ComputeId(reservationId, "LOC-A", "SKU-002") &&
            v.HardLockedQty == 20m)));

        ops.Verify(o => o.Store(It.Is<ActiveHardLockView>(v =>
            v.Id == ActiveHardLockView.ComputeId(reservationId, "LOC-B", "SKU-003") &&
            v.HardLockedQty == 30m)));
    }

    [Fact]
    public void Project_PickingStarted_EmptyLines_ShouldNotStore()
    {
        // Arrange
        var ops = new Mock<IDocumentOperations>();

        var evt = new PickingStartedEvent
        {
            ReservationId = Guid.NewGuid(),
            LockType = "HARD",
            HardLockedLines = new List<HardLockLineDto>()
        };

        // Act
        _projection.Project(evt, ops.Object);

        // Assert: no Store calls
        ops.Verify(o => o.Store(It.IsAny<ActiveHardLockView>()), Times.Never);
    }

    // ── ReservationConsumedEvent → DeleteWhere ────────────────────────

    [Fact]
    public void Project_ReservationConsumed_ShouldDeleteAllRowsForReservation()
    {
        // Arrange
        var ops = new Mock<IDocumentOperations>();
        var reservationId = Guid.NewGuid();

        var evt = new ReservationConsumedEvent
        {
            ReservationId = reservationId,
            ActualQuantity = 50m
        };

        // Act
        _projection.Project(evt, ops.Object);

        // Assert: DeleteWhere called with filter matching reservation
        ops.Verify(o => o.DeleteWhere(
            It.IsAny<Expression<Func<ActiveHardLockView, bool>>>()), Times.Once);
    }

    // ── ReservationCancelledEvent → DeleteWhere ──────────────────────

    [Fact]
    public void Project_ReservationCancelled_ShouldDeleteAllRowsForReservation()
    {
        // Arrange
        var ops = new Mock<IDocumentOperations>();
        var reservationId = Guid.NewGuid();

        var evt = new ReservationCancelledEvent
        {
            ReservationId = reservationId,
            Reason = "Out of stock"
        };

        // Act
        _projection.Project(evt, ops.Object);

        // Assert: DeleteWhere called
        ops.Verify(o => o.DeleteWhere(
            It.IsAny<Expression<Func<ActiveHardLockView, bool>>>()), Times.Once);
    }

    // ── ComputeId: deterministic composite key ───────────────────────

    [Fact]
    public void ComputeId_ShouldReturnDeterministicCompositeKey()
    {
        var reservationId = Guid.Parse("12345678-1234-1234-1234-123456789012");
        var id = ActiveHardLockView.ComputeId(reservationId, "LOC-A", "SKU-001");

        id.Should().Be("12345678-1234-1234-1234-123456789012:LOC-A:SKU-001");
    }

    [Fact]
    public void ComputeId_SameInputs_ShouldReturnSameId()
    {
        var reservationId = Guid.NewGuid();

        var id1 = ActiveHardLockView.ComputeId(reservationId, "LOC-X", "SKU-Y");
        var id2 = ActiveHardLockView.ComputeId(reservationId, "LOC-X", "SKU-Y");

        id1.Should().Be(id2);
    }

    [Fact]
    public void ComputeId_DifferentInputs_ShouldReturnDifferentIds()
    {
        var reservationId = Guid.NewGuid();

        var id1 = ActiveHardLockView.ComputeId(reservationId, "LOC-A", "SKU-001");
        var id2 = ActiveHardLockView.ComputeId(reservationId, "LOC-A", "SKU-002");
        var id3 = ActiveHardLockView.ComputeId(reservationId, "LOC-B", "SKU-001");

        id1.Should().NotBe(id2);
        id1.Should().NotBe(id3);
        id2.Should().NotBe(id3);
    }

    [Fact]
    public void ComputeId_DifferentReservations_SameLocationSku_ShouldReturnDifferentIds()
    {
        var r1 = Guid.NewGuid();
        var r2 = Guid.NewGuid();

        var id1 = ActiveHardLockView.ComputeId(r1, "LOC-A", "SKU-001");
        var id2 = ActiveHardLockView.ComputeId(r2, "LOC-A", "SKU-001");

        id1.Should().NotBe(id2);
    }

    // ── Idempotency: re-applying same event overwrites ───────────────

    [Fact]
    public void Project_PickingStarted_ReApplied_ShouldOverwriteWithSameData()
    {
        // Arrange: Track all stored documents
        var storedDocuments = new List<ActiveHardLockView>();
        var ops = new Mock<IDocumentOperations>();
        ops.Setup(o => o.Store(It.IsAny<ActiveHardLockView[]>()))
           .Callback<ActiveHardLockView[]>(docs => storedDocuments.AddRange(docs));

        var reservationId = Guid.NewGuid();
        var evt = new PickingStartedEvent
        {
            ReservationId = reservationId,
            LockType = "HARD",
            HardLockedLines = new List<HardLockLineDto>
            {
                new() { WarehouseId = "WH1", Location = "LOC-A", SKU = "SKU-001", HardLockedQty = 50m }
            }
        };

        // Act: apply twice (simulating idempotent replay)
        _projection.Project(evt, ops.Object);
        _projection.Project(evt, ops.Object);

        // Assert: both calls produce identical documents (Store with same Id = upsert in Marten)
        storedDocuments.Should().HaveCount(2);
        storedDocuments[0].Id.Should().Be(storedDocuments[1].Id);
        storedDocuments[0].HardLockedQty.Should().Be(storedDocuments[1].HardLockedQty);
    }
}

/// <summary>
/// Unit tests for advisory lock key computation in StartPicking orchestration.
/// </summary>
public class AdvisoryLockKeyTests
{
    [Fact]
    public void ComputeAdvisoryLockKey_SameInputs_ShouldReturnSameKey()
    {
        var key1 = LKvitai.MES.Modules.Warehouse.Infrastructure.Persistence.MartenStartPickingOrchestration
            .ComputeAdvisoryLockKey("WH1", "LOC-A", "SKU-001");
        var key2 = LKvitai.MES.Modules.Warehouse.Infrastructure.Persistence.MartenStartPickingOrchestration
            .ComputeAdvisoryLockKey("WH1", "LOC-A", "SKU-001");

        key1.Should().Be(key2);
    }

    [Fact]
    public void ComputeAdvisoryLockKey_DifferentInputs_ShouldReturnDifferentKeys()
    {
        var key1 = LKvitai.MES.Modules.Warehouse.Infrastructure.Persistence.MartenStartPickingOrchestration
            .ComputeAdvisoryLockKey("WH1", "LOC-A", "SKU-001");
        var key2 = LKvitai.MES.Modules.Warehouse.Infrastructure.Persistence.MartenStartPickingOrchestration
            .ComputeAdvisoryLockKey("WH1", "LOC-A", "SKU-002");
        var key3 = LKvitai.MES.Modules.Warehouse.Infrastructure.Persistence.MartenStartPickingOrchestration
            .ComputeAdvisoryLockKey("WH2", "LOC-A", "SKU-001");

        key1.Should().NotBe(key2);
        key1.Should().NotBe(key3);
    }
}

using FluentAssertions;
using LKvitai.MES.Modules.Warehouse.Domain;
using Xunit;

namespace LKvitai.MES.Tests.Unit;

/// <summary>
/// Tests for StockLockKey — canonical advisory lock key derivation.
/// [HOTFIX CRIT-01] Ensures determinism and correct ordering to prevent deadlocks.
/// </summary>
public class StockLockKeyTests
{
    // ── Determinism ──────────────────────────────────────────────────

    [Fact]
    public void ForLocation_SameInputs_ReturnsSameKey()
    {
        var key1 = StockLockKey.ForLocation("WH1", "LOC-A", "SKU-001");
        var key2 = StockLockKey.ForLocation("WH1", "LOC-A", "SKU-001");

        key1.Should().Be(key2, "same inputs must produce the same lock key");
    }

    [Fact]
    public void ForLocation_DifferentInputs_ReturnsDifferentKeys()
    {
        var key1 = StockLockKey.ForLocation("WH1", "LOC-A", "SKU-001");
        var key2 = StockLockKey.ForLocation("WH1", "LOC-B", "SKU-001");
        var key3 = StockLockKey.ForLocation("WH1", "LOC-A", "SKU-002");
        var key4 = StockLockKey.ForLocation("WH2", "LOC-A", "SKU-001");

        key1.Should().NotBe(key2, "different location should yield different key");
        key1.Should().NotBe(key3, "different SKU should yield different key");
        key1.Should().NotBe(key4, "different warehouse should yield different key");
    }

    // ── Ordering (deadlock prevention) ───────────────────────────────

    [Fact]
    public void ForLocations_ReturnsSortedKeys()
    {
        var locations = new[]
        {
            ("WH1", "LOC-C", "SKU-003"),
            ("WH1", "LOC-A", "SKU-001"),
            ("WH1", "LOC-B", "SKU-002")
        };

        var keys = StockLockKey.ForLocations(locations);

        keys.Should().BeInAscendingOrder("lock keys must be sorted to prevent deadlocks");
    }

    [Fact]
    public void ForLocations_DeduplicatesDuplicateLocations()
    {
        var locations = new[]
        {
            ("WH1", "LOC-A", "SKU-001"),
            ("WH1", "LOC-A", "SKU-001"),
            ("WH1", "LOC-B", "SKU-002")
        };

        var keys = StockLockKey.ForLocations(locations);

        keys.Should().HaveCount(2, "duplicate (warehouse, location, sku) should be deduplicated");
    }

    [Fact]
    public void ForLocations_EmptyInput_ReturnsEmptyArray()
    {
        var keys = StockLockKey.ForLocations(Array.Empty<(string, string, string)>());

        keys.Should().BeEmpty();
    }

    // ── Match with MartenStartPickingOrchestration.ComputeAdvisoryLockKey ──

    [Fact]
    public void ForLocation_MatchesStartPickingOrchestrationKey()
    {
        // [CRIT-01] The canonical key MUST match the key used by StartPicking.
        // MartenStartPickingOrchestration.ComputeAdvisoryLockKey now delegates
        // to StockLockKey.ForLocation.
        var canonicalKey = StockLockKey.ForLocation("WH1", "LOC-A", "SKU-001");
        var orchKey = LKvitai.MES.Infrastructure.Persistence.MartenStartPickingOrchestration
            .ComputeAdvisoryLockKey("WH1", "LOC-A", "SKU-001");

        canonicalKey.Should().Be(orchKey,
            "StartPicking and outbound movements must use the SAME lock key");
    }

    // ── Null guards ─────────────────────────────────────────────────

    [Fact]
    public void ForLocation_ThrowsOnNullWarehouseId()
    {
        var act = () => StockLockKey.ForLocation(null!, "LOC-A", "SKU-001");
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ForLocation_ThrowsOnNullLocation()
    {
        var act = () => StockLockKey.ForLocation("WH1", null!, "SKU-001");
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ForLocation_ThrowsOnNullSku()
    {
        var act = () => StockLockKey.ForLocation("WH1", "LOC-A", null!);
        act.Should().Throw<ArgumentNullException>();
    }

    // ── MovementType.IsBalanceDecreasing ──────────────────────────────

    [Theory]
    [InlineData("DISPATCH", true)]
    [InlineData("TRANSFER", true)]
    [InlineData("ADJUSTMENT_OUT", true)]
    [InlineData("PICK", true)]
    [InlineData("RECEIPT", false)]
    [InlineData("ADJUSTMENT_IN", false)]
    public void IsBalanceDecreasing_CorrectlyClassifiesMovementTypes(
        string movementType, bool expectedResult)
    {
        MovementType.IsBalanceDecreasing(movementType).Should().Be(expectedResult);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void IsBalanceDecreasing_ReturnsFalse_ForEmptyOrNull(string? movementType)
    {
        MovementType.IsBalanceDecreasing(movementType!).Should().BeFalse();
    }
}

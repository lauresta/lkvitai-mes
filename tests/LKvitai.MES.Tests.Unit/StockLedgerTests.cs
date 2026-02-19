using FluentAssertions;
using LKvitai.MES.Contracts.Events;
using LKvitai.MES.Domain;
using LKvitai.MES.Domain.Aggregates;
using LKvitai.MES.SharedKernel;
using Xunit;

namespace LKvitai.MES.Tests.Unit;

/// <summary>
/// Unit tests for StockLedger aggregate — domain invariants and event application.
/// </summary>
public class StockLedgerTests
{
    private readonly StockLedger _ledger = new();
    private readonly Guid _operatorId = Guid.NewGuid();

    // ── RecordMovement: positive-path ────────────────────────────────────

    [Fact]
    public void RecordMovement_Receipt_ShouldProduceEvent()
    {
        var evt = _ledger.RecordMovement(
            Guid.NewGuid(), "SKU-A", 10m, "", "LOC-1",
            MovementType.Receipt, _operatorId);

        evt.Should().NotBeNull();
        evt.SKU.Should().Be("SKU-A");
        evt.Quantity.Should().Be(10m);
        evt.ToLocation.Should().Be("LOC-1");
        evt.MovementType.Should().Be(MovementType.Receipt);
    }

    [Fact]
    public void RecordMovement_Transfer_ShouldProduceEvent_WhenBalanceSufficient()
    {
        // Arrange: seed balance via receipt
        _ledger.Apply(MakeReceipt("LOC-A", "SKU-X", 20m));

        // Act: transfer out 15
        var evt = _ledger.RecordMovement(
            Guid.NewGuid(), "SKU-X", 15m, "LOC-A", "LOC-B",
            MovementType.Transfer, _operatorId);

        evt.Quantity.Should().Be(15m);
        evt.FromLocation.Should().Be("LOC-A");
        evt.ToLocation.Should().Be("LOC-B");
    }

    [Fact]
    public void RecordMovement_Dispatch_ShouldProduceEvent_WhenBalanceSufficient()
    {
        _ledger.Apply(MakeReceipt("LOC-A", "SKU-Y", 50m));

        var evt = _ledger.RecordMovement(
            Guid.NewGuid(), "SKU-Y", 50m, "LOC-A", "",
            MovementType.Dispatch, _operatorId);

        evt.Quantity.Should().Be(50m);
    }

    // ── RecordMovement: invariant violations ─────────────────────────────

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100.5)]
    public void RecordMovement_ShouldReject_ZeroOrNegativeQuantity(decimal qty)
    {
        var act = () => _ledger.RecordMovement(
            Guid.NewGuid(), "SKU-A", qty, "", "LOC-1",
            MovementType.Receipt, _operatorId);

        act.Should().Throw<DomainException>()
           .WithMessage("*quantity must be greater than zero*");
    }

    [Fact]
    public void RecordMovement_ShouldReject_SameFromAndTo_ForTransfer()
    {
        _ledger.Apply(MakeReceipt("LOC-A", "SKU-A", 100m));

        var act = () => _ledger.RecordMovement(
            Guid.NewGuid(), "SKU-A", 10m, "LOC-A", "LOC-A",
            MovementType.Transfer, _operatorId);

        act.Should().Throw<DomainException>()
           .WithMessage("*must differ from to location*");
    }

    [Fact]
    public void RecordMovement_ShouldReject_InsufficientBalance()
    {
        _ledger.Apply(MakeReceipt("LOC-A", "SKU-A", 5m));

        var act = () => _ledger.RecordMovement(
            Guid.NewGuid(), "SKU-A", 10m, "LOC-A", "LOC-B",
            MovementType.Transfer, _operatorId);

        act.Should().Throw<InsufficientBalanceException>()
           .Where(ex => ex.AvailableQuantity == 5m && ex.RequestedQuantity == 10m);
    }

    [Fact]
    public void RecordMovement_ShouldReject_Dispatch_FromEmptyLocation()
    {
        // No stock at LOC-X
        var act = () => _ledger.RecordMovement(
            Guid.NewGuid(), "SKU-A", 1m, "LOC-X", "",
            MovementType.Dispatch, _operatorId);

        act.Should().Throw<InsufficientBalanceException>()
           .Where(ex => ex.AvailableQuantity == 0m);
    }

    // ── Apply: balance tracking ──────────────────────────────────────────

    [Fact]
    public void Apply_Receipt_ShouldIncreaseToBalance()
    {
        _ledger.Apply(MakeReceipt("LOC-1", "SKU-A", 100m));

        _ledger.GetBalance("LOC-1", "SKU-A").Should().Be(100m);
    }

    [Fact]
    public void Apply_Transfer_ShouldDecreaseFromAndIncreaseTo()
    {
        _ledger.Apply(MakeReceipt("LOC-A", "SKU-A", 50m));
        _ledger.Apply(MakeTransfer("LOC-A", "LOC-B", "SKU-A", 20m));

        _ledger.GetBalance("LOC-A", "SKU-A").Should().Be(30m);
        _ledger.GetBalance("LOC-B", "SKU-A").Should().Be(20m);
    }

    [Fact]
    public void Apply_MultipleSKUs_ShouldTrackIndependently()
    {
        _ledger.Apply(MakeReceipt("LOC-1", "SKU-A", 10m));
        _ledger.Apply(MakeReceipt("LOC-1", "SKU-B", 20m));

        _ledger.GetBalance("LOC-1", "SKU-A").Should().Be(10m);
        _ledger.GetBalance("LOC-1", "SKU-B").Should().Be(20m);
    }

    [Fact]
    public void Apply_MultipleLocations_ShouldTrackIndependently()
    {
        _ledger.Apply(MakeReceipt("LOC-1", "SKU-A", 10m));
        _ledger.Apply(MakeReceipt("LOC-2", "SKU-A", 30m));

        _ledger.GetBalance("LOC-1", "SKU-A").Should().Be(10m);
        _ledger.GetBalance("LOC-2", "SKU-A").Should().Be(30m);
    }

    [Fact]
    public void GetBalance_UnknownLocationSku_ShouldReturnZero()
    {
        _ledger.GetBalance("NOWHERE", "NOTHING").Should().Be(0m);
    }

    // ── GetAllBalances ───────────────────────────────────────────────────

    [Fact]
    public void GetAllBalances_ShouldReturnSnapshot()
    {
        _ledger.Apply(MakeReceipt("LOC-1", "SKU-A", 10m));
        _ledger.Apply(MakeReceipt("LOC-2", "SKU-B", 20m));

        var balances = _ledger.GetAllBalances();
        balances.Should().HaveCount(2);
        balances["LOC-1:SKU-A"].Should().Be(10m);
        balances["LOC-2:SKU-B"].Should().Be(20m);
    }

    // ── Receipt does not require FROM balance ────────────────────────────

    [Fact]
    public void RecordMovement_Receipt_ShouldNotCheckFromBalance()
    {
        // No prior balance, but receipt should succeed
        var evt = _ledger.RecordMovement(
            Guid.NewGuid(), "SKU-NEW", 1000m, "", "LOC-1",
            MovementType.Receipt, _operatorId);

        evt.Should().NotBeNull();
    }

    [Fact]
    public void RecordMovement_AdjustmentIn_ShouldNotCheckFromBalance()
    {
        var evt = _ledger.RecordMovement(
            Guid.NewGuid(), "SKU-ADJ", 500m, "", "LOC-1",
            MovementType.AdjustmentIn, _operatorId);

        evt.Should().NotBeNull();
    }

    // ── Exact balance transfer (drain to zero) ───────────────────────────

    [Fact]
    public void RecordMovement_ShouldAllow_ExactBalanceTransfer()
    {
        _ledger.Apply(MakeReceipt("LOC-A", "SKU-X", 25m));

        var evt = _ledger.RecordMovement(
            Guid.NewGuid(), "SKU-X", 25m, "LOC-A", "LOC-B",
            MovementType.Transfer, _operatorId);

        evt.Quantity.Should().Be(25m);
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private static StockMovedEvent MakeReceipt(string toLocation, string sku, decimal qty)
        => new()
        {
            MovementId = Guid.NewGuid(),
            SKU = sku,
            Quantity = qty,
            FromLocation = "",
            ToLocation = toLocation,
            MovementType = MovementType.Receipt,
            OperatorId = Guid.NewGuid(),
            Timestamp = DateTime.UtcNow
        };

    private static StockMovedEvent MakeTransfer(
        string from, string to, string sku, decimal qty)
        => new()
        {
            MovementId = Guid.NewGuid(),
            SKU = sku,
            Quantity = qty,
            FromLocation = from,
            ToLocation = to,
            MovementType = MovementType.Transfer,
            OperatorId = Guid.NewGuid(),
            Timestamp = DateTime.UtcNow
        };
}

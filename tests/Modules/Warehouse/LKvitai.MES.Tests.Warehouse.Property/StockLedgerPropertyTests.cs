using FsCheck;
using FsCheck.Xunit;
using FluentAssertions;
using LKvitai.MES.Contracts.Events;
using LKvitai.MES.Modules.Warehouse.Domain;
using LKvitai.MES.Modules.Warehouse.Domain.Aggregates;
using LKvitai.MES.BuildingBlocks.SharedKernel;
using Xunit;

namespace LKvitai.MES.Tests.Warehouse.Property;

/// <summary>
/// Property-based tests for StockLedger invariants.
/// Minimum 100 iterations per property test per blueprint.
///
/// Properties tested:
///   P1: No negative balance — any sequence of valid receipt+transfer events never produces negative balances
///   P2: Quantity must be positive — RecordMovement always rejects qty ≤ 0
///   P3: Balance conservation — total stock across all locations is conserved for internal transfers
///   P4: From ≠ To — transfer with same from/to is always rejected
///   P5: Insufficient balance detection — transfer exceeding balance is always rejected
/// </summary>
public class StockLedgerPropertyTests
{
    // ── Generators ───────────────────────────────────────────────────────

    private static readonly string[] Locations = { "LOC-A", "LOC-B", "LOC-C", "LOC-D" };
    private static readonly string[] SKUs = { "SKU-1", "SKU-2", "SKU-3" };

    private static Gen<string> LocationGen =>
        Gen.Elements(Locations);

    private static Gen<string> SkuGen =>
        Gen.Elements(SKUs);

    private static Gen<decimal> PositiveQtyGen =>
        Gen.Choose(1, 1000).Select(i => (decimal)i);

    // ── P1: No negative balance ──────────────────────────────────────────

    [Property(MaxTest = 200)]
    public FsCheck.Property NoNegativeBalance_AfterAnyValidEventSequence()
    {
        var gen = GenValidEventSequence(5, 20);

        return Prop.ForAll(gen.ToArbitrary(), events =>
        {
            var ledger = new StockLedger();
            foreach (var evt in events)
            {
                ledger.Apply(evt);
            }

            var balances = ledger.GetAllBalances();
            foreach (var kvp in balances)
            {
                kvp.Value.Should().BeGreaterThanOrEqualTo(0m,
                    $"Balance for {kvp.Key} should never be negative, was {kvp.Value}");
            }
        });
    }

    // ── P2: Quantity must be positive ────────────────────────────────────

    [Property(MaxTest = 100)]
    public FsCheck.Property RecordMovement_AlwaysRejects_NonPositiveQuantity(int rawQty)
    {
        // Generate non-positive values
        var qty = rawQty <= 0 ? (decimal)rawQty : -(decimal)rawQty;

        return Prop.ForAll(Arb.Default.Bool().Generator.ToArbitrary(), _ =>
        {
            var ledger = new StockLedger();
            var act = () => ledger.RecordMovement(
                Guid.NewGuid(), "SKU-1", qty, "", "LOC-A",
                MovementType.Receipt, Guid.NewGuid());

            act.Should().Throw<DomainException>();
        });
    }

    // ── P3: Balance conservation for internal transfers ──────────────────

    [Property(MaxTest = 200)]
    public FsCheck.Property TotalStock_IsConserved_ByInternalTransfers()
    {
        var gen = GenValidEventSequence(3, 15);

        return Prop.ForAll(gen.ToArbitrary(), events =>
        {
            var ledger = new StockLedger();

            // Track total receipts per SKU (external stock entering the system)
            var totalReceipts = new Dictionary<string, decimal>();

            foreach (var evt in events)
            {
                if (evt.MovementType == MovementType.Receipt)
                {
                    totalReceipts[evt.SKU] = totalReceipts.GetValueOrDefault(evt.SKU, 0m) + evt.Quantity;
                }

                ledger.Apply(evt);
            }

            // For each SKU, sum of balances across all locations should equal total receipts
            var balances = ledger.GetAllBalances();
            foreach (var sku in SKUs)
            {
                var totalBalance = balances
                    .Where(kvp => kvp.Key.EndsWith($":{sku}"))
                    .Sum(kvp => kvp.Value);

                var expectedTotal = totalReceipts.GetValueOrDefault(sku, 0m);
                totalBalance.Should().Be(expectedTotal,
                    $"Total stock for {sku} should equal total receipts ({expectedTotal})");
            }
        });
    }

    // ── P4: From ≠ To always rejected for transfers ─────────────────────

    [Property(MaxTest = 100)]
    public FsCheck.Property SameFromTo_AlwaysRejected_ForTransfer()
    {
        return Prop.ForAll(LocationGen.ToArbitrary(), SkuGen.ToArbitrary(),
            (location, sku) =>
            {
                var ledger = new StockLedger();
                // Seed balance so rejection is for from==to, not insufficient balance
                ledger.Apply(new StockMovedEvent
                {
                    MovementId = Guid.NewGuid(),
                    SKU = sku,
                    Quantity = 1000m,
                    FromLocation = "",
                    ToLocation = location,
                    MovementType = MovementType.Receipt,
                    OperatorId = Guid.NewGuid()
                });

                var act = () => ledger.RecordMovement(
                    Guid.NewGuid(), sku, 1m, location, location,
                    MovementType.Transfer, Guid.NewGuid());

                act.Should().Throw<DomainException>()
                   .WithMessage("*must differ*");
            });
    }

    // ── P5: Insufficient balance always detected ─────────────────────────

    [Property(MaxTest = 100)]
    public FsCheck.Property InsufficientBalance_AlwaysDetected()
    {
        // Combine (location, sku) and (seedQty, extraQty) into two tuple generators
        var locSkuGen = LocationGen.SelectMany(loc => SkuGen.Select(sku => (loc, sku)));
        var qtyPairGen = PositiveQtyGen.SelectMany(seed => PositiveQtyGen.Select(extra => (seed, extra)));

        return Prop.ForAll(
            locSkuGen.ToArbitrary(),
            qtyPairGen.ToArbitrary(),
            (locSku, qtyPair) =>
            {
                var (location, sku) = locSku;
                var (seedQty, extraQty) = qtyPair;

                var ledger = new StockLedger();
                ledger.Apply(new StockMovedEvent
                {
                    MovementId = Guid.NewGuid(),
                    SKU = sku,
                    Quantity = seedQty,
                    FromLocation = "",
                    ToLocation = location,
                    MovementType = MovementType.Receipt,
                    OperatorId = Guid.NewGuid()
                });

                // Try to transfer more than available
                var transferQty = seedQty + extraQty; // Always > seedQty

                var act = () => ledger.RecordMovement(
                    Guid.NewGuid(), sku, transferQty, location, "OTHER",
                    MovementType.Transfer, Guid.NewGuid());

                act.Should().Throw<InsufficientBalanceException>();
            });
    }

    // ── Event sequence generator ─────────────────────────────────────────

    /// <summary>
    /// Generates a sequence of StockMovedEvents that are VALID:
    /// - Starts with receipts to seed stock
    /// - Then interleaves receipts and transfers that respect balance constraints
    /// This ensures the property tests can focus on the INVARIANT (no negative balance)
    /// rather than event generation validity.
    /// </summary>
    private static Gen<List<StockMovedEvent>> GenValidEventSequence(int minReceipts, int maxTotal)
    {
        return Gen.Choose(minReceipts, maxTotal).SelectMany(count =>
        {
            return Gen.Constant(0).Select(_ =>
            {
                var rng = new System.Random();
                var events = new List<StockMovedEvent>();
                var balances = new Dictionary<string, decimal>();

                // Phase 1: seed with receipts
                for (int i = 0; i < minReceipts; i++)
                {
                    var loc = Locations[rng.Next(Locations.Length)];
                    var sku = SKUs[rng.Next(SKUs.Length)];
                    var qty = (decimal)rng.Next(10, 200);

                    events.Add(new StockMovedEvent
                    {
                        MovementId = Guid.NewGuid(),
                        SKU = sku,
                        Quantity = qty,
                        FromLocation = "",
                        ToLocation = loc,
                        MovementType = MovementType.Receipt,
                        OperatorId = Guid.NewGuid(),
                        Timestamp = DateTime.UtcNow
                    });

                    var key = $"{loc}:{sku}";
                    balances[key] = balances.GetValueOrDefault(key, 0m) + qty;
                }

                // Phase 2: mix receipts and valid transfers
                for (int i = minReceipts; i < count; i++)
                {
                    var sku = SKUs[rng.Next(SKUs.Length)];

                    if (rng.NextDouble() < 0.4) // 40% receipts
                    {
                        var loc = Locations[rng.Next(Locations.Length)];
                        var qty = (decimal)rng.Next(1, 100);

                        events.Add(new StockMovedEvent
                        {
                            MovementId = Guid.NewGuid(),
                            SKU = sku,
                            Quantity = qty,
                            FromLocation = "",
                            ToLocation = loc,
                            MovementType = MovementType.Receipt,
                            OperatorId = Guid.NewGuid(),
                            Timestamp = DateTime.UtcNow
                        });

                        var key = $"{loc}:{sku}";
                        balances[key] = balances.GetValueOrDefault(key, 0m) + qty;
                    }
                    else // 60% transfers
                    {
                        // Find a location with balance for this SKU
                        var candidates = Locations
                            .Where(l => balances.GetValueOrDefault($"{l}:{sku}", 0m) > 0)
                            .ToList();

                        if (candidates.Count == 0) continue;

                        var from = candidates[rng.Next(candidates.Count)];
                        var to = Locations.Where(l => l != from).ToArray();
                        var toLoc = to[rng.Next(to.Length)];

                        var available = balances[$"{from}:{sku}"];
                        var qty = Math.Min((decimal)rng.Next(1, 50), available);
                        if (qty <= 0) continue;

                        events.Add(new StockMovedEvent
                        {
                            MovementId = Guid.NewGuid(),
                            SKU = sku,
                            Quantity = qty,
                            FromLocation = from,
                            ToLocation = toLoc,
                            MovementType = MovementType.Transfer,
                            OperatorId = Guid.NewGuid(),
                            Timestamp = DateTime.UtcNow
                        });

                        balances[$"{from}:{sku}"] -= qty;
                        balances[$"{toLoc}:{sku}"] = balances.GetValueOrDefault($"{toLoc}:{sku}", 0m) + qty;
                    }
                }

                return events;
            });
        });
    }
}

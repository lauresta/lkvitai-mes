using FluentAssertions;
using FsCheck;
using FsCheck.Xunit;
using LKvitai.MES.Contracts.Events;
using LKvitai.MES.Modules.Warehouse.Domain.Aggregates;
using LKvitai.MES.BuildingBlocks.SharedKernel;
using Xunit;

namespace LKvitai.MES.Tests.Warehouse.Property;

/// <summary>W1 acceptance: the value ledger of an item (blueprint 4.1).</summary>
public class ItemValuationPropertyTests
{
    private const int ItemId = 7;
    private static readonly StockValueDocument Doc = new("Test", "1", "T-1", "property test");

    // Operation codes: 0 receipt, 1 issue, 2 return in, 3 adjustment in, 4 adjustment out, 5 scrap, 6 revaluation
    private static StockValueChanged? Step(ItemValuation v, int op, int qtyUnits, int priceCents)
    {
        var qty = (qtyUnits % 500 + 1) / 4m;                  // 0.25 .. 125.00
        var price = (priceCents % 100000 + 1) / 100m;          // 0.01 .. 1000.00
        try
        {
            return (op % 7) switch
            {
                0 => v.Inflow(ItemId, StockValueKinds.Receipt, qty, ItemValuation.Money(qty * price), Doc),
                1 => v.Outflow(ItemId, StockValueKinds.Issue, qty, Doc),
                2 => v.InflowAtAverage(ItemId, StockValueKinds.ReturnIn, qty, Doc),
                3 => v.InflowAtAverage(ItemId, StockValueKinds.AdjustmentIn, qty, Doc),
                4 => v.Outflow(ItemId, StockValueKinds.AdjustmentOut, qty, Doc),
                5 => v.Outflow(ItemId, StockValueKinds.Scrap, qty, Doc),
                _ => v.Revalue(ItemId, v.UnitCost / 2m, Doc)
            };
        }
        catch (DomainException)
        {
            return null; // a refused operation changes nothing
        }
    }

    [FsCheck.Xunit.Property(MaxTest = 300)]
    public bool SumOfValueDeltasEqualsValue_AndValueNeverNegative(int[] ops, int[] qtys, int[] prices)
    {
        var v = new ItemValuation();
        var sumValue = 0m;
        var sumQty = 0m;
        var n = new[] { ops?.Length ?? 0, qtys?.Length ?? 0, prices?.Length ?? 0 }.Min();
        for (var i = 0; i < n; i++)
        {
            var evt = Step(v, Math.Abs(ops![i] % 7), Math.Abs(qtys![i] % 1000), Math.Abs(prices![i] % 100000));
            if (evt is null)
            {
                continue;
            }

            v.Apply(evt);
            sumValue += evt.ValueDeltaEur;
            sumQty += evt.QtyDelta;
            if (v.ValueEur < 0m || v.ValuedQty < 0m)
            {
                return false;
            }
        }

        return sumValue == v.ValueEur && sumQty == v.ValuedQty;
    }

    [FsCheck.Xunit.Property(MaxTest = 200)]
    public bool IssuingAllStockGivesZeroValue(int[] qtys, int[] prices)
    {
        var v = new ItemValuation();
        var n = Math.Min(qtys?.Length ?? 0, prices?.Length ?? 0);
        for (var i = 0; i < n; i++)
        {
            v.Apply(Step(v, 0, Math.Abs(qtys![i] % 1000), Math.Abs(prices![i] % 100000))!);
        }

        if (v.ValuedQty == 0m)
        {
            return v.ValueEur == 0m;
        }

        var issue = v.Outflow(ItemId, StockValueKinds.Issue, v.ValuedQty, Doc)!;
        v.Apply(issue);
        return v.ValueEur == 0m && v.ValuedQty == 0m;
    }

    [FsCheck.Xunit.Property(MaxTest = 200)]
    public bool IssuingMoreThanValuedTakesTheWholeValueOnly(int[] qtys, int[] prices)
    {
        var v = new ItemValuation();
        var n = Math.Min(qtys?.Length ?? 0, prices?.Length ?? 0);
        for (var i = 0; i < n; i++)
        {
            v.Apply(Step(v, 0, Math.Abs(qtys![i] % 1000), Math.Abs(prices![i] % 100000))!);
        }

        if (v.ValuedQty == 0m)
        {
            return true;
        }

        var issue = v.Outflow(ItemId, StockValueKinds.Issue, v.ValuedQty + 10m, Doc)!;
        v.Apply(issue);
        return v.ValueEur == 0m && v.ValuedQty == 0m;
    }

    [Fact]
    public void ConcurrentReceipts_GiveTheSameValueAsSequentialOnes()
    {
        var receipts = Enumerable.Range(1, 24)
            .Select(i => (Qty: i * 1.5m, Value: ItemValuation.Money(i * 1.5m * (3m + i / 7m))))
            .ToArray();

        var sequential = new ItemValuation();
        foreach (var (qty, value) in receipts)
        {
            sequential.Apply(sequential.Inflow(ItemId, StockValueKinds.Receipt, qty, value, Doc));
        }

        // Writers race on one stream: an append succeeds only with the version it read (optimistic check) and retries otherwise.
        var stream = new List<StockValueChanged>();
        var gate = new object();
        var retries = 0;
        Parallel.ForEach(receipts, new ParallelOptions { MaxDegreeOfParallelism = 8 }, receipt =>
        {
            while (true)
            {
                int readVersion;
                var state = new ItemValuation();
                lock (gate)
                {
                    readVersion = stream.Count;
                    foreach (var e in stream) state.Apply(e);
                }

                var evt = state.Inflow(ItemId, StockValueKinds.Receipt, receipt.Qty, receipt.Value, Doc);
                lock (gate)
                {
                    if (stream.Count == readVersion)
                    {
                        stream.Add(evt);
                        return;
                    }

                    retries++;
                }
            }
        });

        var concurrent = new ItemValuation();
        foreach (var e in stream) concurrent.Apply(e);

        stream.Should().HaveCount(receipts.Length);
        concurrent.ValueEur.Should().Be(sequential.ValueEur);
        concurrent.ValuedQty.Should().Be(sequential.ValuedQty);
    }

    [Fact]
    public void Receipt_480_At_3_61_OntoExistingStock_RaisesValueByExactly_1732_80()
    {
        var v = new ItemValuation();
        v.Apply(v.Inflow(ItemId, StockValueKinds.Opening, 1237.5m, ItemValuation.Money(1237.5m * 3.4567m), Doc));
        var before = v.ValueEur;

        var receipt = v.Inflow(ItemId, StockValueKinds.Receipt, 480m, ItemValuation.Money(480m * 3.61m), Doc);
        v.Apply(receipt);

        receipt.ValueDeltaEur.Should().Be(1732.80m);
        (v.ValueEur - before).Should().Be(1732.80m);
        v.ValuedQty.Should().Be(1717.5m);
    }
}

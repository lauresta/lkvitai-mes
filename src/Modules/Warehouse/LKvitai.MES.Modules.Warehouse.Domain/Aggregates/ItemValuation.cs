using LKvitai.MES.Contracts.Events;
using LKvitai.MES.BuildingBlocks.SharedKernel;

namespace LKvitai.MES.Modules.Warehouse.Domain.Aggregates;

/// <summary>Document that caused a value change.</summary>
public sealed record StockValueDocument(
    string? Type,
    string? Id,
    string? Number,
    string? Reason,
    int? StockAccountProfileId = null,
    DateTime? TimestampUtc = null);

/// <summary>
/// Event-sourced value ledger of one inventory item: valued quantity and value in EUR (blueprint 4.1).
/// The unit cost is derived from the value, never stored.
/// </summary>
public sealed class ItemValuation
{
    private const int Scale = 4;

    /// <summary>Marten snapshot identity - matches the event stream id.</summary>
    public string Id { get; private set; } = string.Empty;

    public int ItemId { get; private set; }
    public decimal ValuedQty { get; private set; }
    public decimal ValueEur { get; private set; }
    /// <summary>Last known unit cost, used while <see cref="ValuedQty"/> is 0.</summary>
    public decimal LastKnownUnitCost { get; private set; }
    public bool IsInitialized { get; private set; }
    public int Version { get; private set; }

    /// <summary>Unit cost shown with 4 decimals; the last known cost when nothing is valued.</summary>
    public decimal UnitCost => ValuedQty > 0m ? Normalize(ValueEur / ValuedQty) : LastKnownUnitCost;

    public static string StreamIdFor(int itemId)
    {
        if (itemId <= 0)
        {
            throw new DomainException(DomainErrorCodes.ValidationError, "ItemId must be greater than zero.");
        }

        return $"valuation-item-{itemId}";
    }

    public static decimal Money(decimal value) => decimal.Round(value, 2, MidpointRounding.AwayFromZero);

    /// <summary>Quantity enters the stock with an explicit value (Opening, Receipt, ReceiptTrueUp, PriceDifference).</summary>
    public StockValueChanged Inflow(int itemId, string kind, decimal qty, decimal valueEur, StockValueDocument document, DateTime? businessDate = null)
    {
        if (qty < 0m)
        {
            throw new DomainException(DomainErrorCodes.ValidationError, "Quantity must be greater than or equal to zero.");
        }

        return Change(itemId, kind, qty, valueEur, document, businessDate);
    }

    /// <summary>Quantity enters the stock at the current average (ReturnIn, AdjustmentIn).</summary>
    public StockValueChanged InflowAtAverage(int itemId, string kind, decimal qty, StockValueDocument document, DateTime? businessDate = null)
    {
        if (qty <= 0m)
        {
            throw new DomainException(DomainErrorCodes.ValidationError, "Quantity must be greater than zero.");
        }

        var average = ValuedQty > 0m ? ValueEur / ValuedQty : LastKnownUnitCost;
        return Change(itemId, kind, qty, Money(qty * average), document, businessDate);
    }

    /// <summary>
    /// Quantity leaves the stock at the current average. The last unit takes the whole remaining value;
    /// quantity beyond the valued quantity carries no value. Returns null when nothing is valued.
    /// </summary>
    public StockValueChanged? Outflow(int itemId, string kind, decimal qty, StockValueDocument document, DateTime? businessDate = null)
    {
        if (qty <= 0m)
        {
            throw new DomainException(DomainErrorCodes.ValidationError, "Quantity must be greater than zero.");
        }

        if (ValuedQty <= 0m)
        {
            return null;
        }

        var taken = Math.Min(qty, ValuedQty);
        var value = taken >= ValuedQty ? ValueEur : Money(taken * ValueEur / ValuedQty);
        return Change(itemId, kind, -taken, -value, document, businessDate);
    }

    /// <summary>Markdown of the valued stock to a lower unit price (Revaluation).</summary>
    public StockValueChanged Revalue(int itemId, decimal newUnitPrice, StockValueDocument document, DateTime? businessDate = null)
    {
        if (newUnitPrice < 0m)
        {
            throw new DomainException(DomainErrorCodes.ValidationError, "New unit price must be greater than or equal to zero.");
        }

        if (ValuedQty <= 0m)
        {
            throw new DomainException(DomainErrorCodes.ValidationError, "There is no valued stock to revalue.");
        }

        var target = Money(newUnitPrice * ValuedQty);
        if (target >= ValueEur)
        {
            throw new DomainException(DomainErrorCodes.ValidationError, "Revaluation must reduce the stock value.");
        }

        return Change(itemId, StockValueKinds.Revaluation, 0m, target - ValueEur, document, businessDate);
    }

    /// <summary>
    /// Posting of a purchase document (blueprint 4.2): the difference between the final and the preliminary value of
    /// <paramref name="receivedQty"/> received units. The share that is still on stock, min(valued, received) / received,
    /// becomes ReceiptTrueUp; the rest has no place in the stock and becomes PriceDifference (off ledger).
    /// </summary>
    public IReadOnlyList<StockValueChanged> TrueUp(int itemId, decimal receivedQty, decimal differenceEur, StockValueDocument document, DateTime? businessDate = null)
    {
        var diff = Money(differenceEur);
        if (diff == 0m || receivedQty <= 0m)
        {
            return [];
        }

        var onStock = Math.Min(ValuedQty, receivedQty);
        var inStock = Money(diff * onStock / receivedQty);
        if (ValueEur + inStock < 0m)
        {
            inStock = -ValueEur;
        }

        var rest = diff - inStock;
        var list = new List<StockValueChanged>();
        if (inStock != 0m)
        {
            list.Add(Change(itemId, StockValueKinds.ReceiptTrueUp, 0m, inStock, document, businessDate));
        }

        if (rest != 0m)
        {
            list.Add(Change(itemId, StockValueKinds.PriceDifference, 0m, 0m, document, businessDate, rest));
        }

        return list;
    }

    /// <summary>
    /// Supplier credit (blueprint 4.8). With <paramref name="returnQty"/> above 0 the goods leave at the average
    /// (SupplierReturn) and the rest of the credit goes down the remaining stock (Revaluation), at most its value;
    /// without goods the credit is split by the share of <paramref name="receivedQty"/> still on stock. What has no
    /// place in the stock is PriceDifference (off ledger, negative for a credit).
    /// </summary>
    public IReadOnlyList<StockValueChanged> SupplierCredit(
        int itemId, decimal returnQty, decimal creditEur, decimal receivedQty, StockValueDocument document, DateTime? businessDate = null)
    {
        var list = new List<StockValueChanged>();
        var remainingQty = ValuedQty;
        var remainingValue = ValueEur;
        var returned = 0m;
        if (returnQty > 0m)
        {
            var outflow = Outflow(itemId, StockValueKinds.SupplierReturn, returnQty, document, businessDate);
            if (outflow is not null)
            {
                list.Add(outflow);
                returned = -outflow.ValueDeltaEur;
                remainingQty += outflow.QtyDelta;
                remainingValue += outflow.ValueDeltaEur;
            }
        }

        var credit = Money(creditEur);
        decimal onStock;
        if (returnQty > 0m)
        {
            onStock = Math.Max(0m, Math.Min(credit - returned, remainingValue));
        }
        else
        {
            var share = receivedQty > 0m ? Money(credit * Math.Min(remainingQty, receivedQty) / receivedQty) : 0m;
            onStock = Math.Min(share, remainingValue);
        }

        var rest = credit - returned - onStock;
        if (onStock != 0m)
        {
            list.Add(Change(itemId, StockValueKinds.Revaluation, 0m, -onStock, document, businessDate));
        }

        if (rest != 0m)
        {
            list.Add(Change(itemId, StockValueKinds.PriceDifference, 0m, 0m, document, businessDate, -rest));
        }

        return list;
    }

    /// <summary>Builds a validated event; throws when it would make the quantity or the value negative.</summary>
    public StockValueChanged Change(int itemId, string kind, decimal qtyDelta, decimal valueDeltaEur, StockValueDocument document, DateTime? businessDate = null, decimal offLedgerEur = 0m)
    {
        StreamIdFor(itemId);
        if (!StockValueKinds.All.Contains(kind))
        {
            throw new DomainException(DomainErrorCodes.ValidationError, $"Unknown stock value kind '{kind}'.");
        }

        var value = Money(valueDeltaEur);
        if (ValuedQty + qtyDelta < 0m)
        {
            throw new DomainException(DomainErrorCodes.ValidationError, "Valued quantity cannot become negative.");
        }

        if (ValueEur + value < 0m)
        {
            throw new DomainException(DomainErrorCodes.ValidationError, "Stock value cannot become negative.");
        }

        var timestamp = EnsureUtc(document.TimestampUtc ?? DateTime.UtcNow);
        return new StockValueChanged
        {
            ItemId = itemId,
            Kind = kind,
            QtyDelta = qtyDelta,
            ValueDeltaEur = value,
            DocumentType = document.Type,
            DocumentId = document.Id,
            DocumentNumber = document.Number,
            StockAccountProfileId = document.StockAccountProfileId,
            BusinessDate = businessDate ?? StockBusinessDate.From(timestamp),
            Reason = document.Reason,
            OffLedgerEur = Money(offLedgerEur),
            Timestamp = timestamp
        };
    }

    public void Apply(StockValueChanged evt)
    {
        ItemId = evt.ItemId;
        Id = StreamIdFor(ItemId);
        ValuedQty += evt.QtyDelta;
        ValueEur += evt.ValueDeltaEur;
        if (ValuedQty > 0m)
        {
            LastKnownUnitCost = Normalize(ValueEur / ValuedQty);
        }

        IsInitialized = true;
        Version++;
    }

    // Legacy events stay readable; they carry no value and the code no longer creates them.
    public void Apply(ValuationInitialized evt)
    {
        ItemId = evt.InventoryItemId > 0
            ? evt.InventoryItemId
            : ValuationItemId.ToInventoryItemId(evt.ItemId);
        Id = StreamIdFor(ItemId);
        LastKnownUnitCost = Normalize(evt.InitialUnitCost);
        IsInitialized = true;
        Version++;
    }

    public void Apply(CostAdjusted evt)
    {
        LastKnownUnitCost = Normalize(evt.NewUnitCost);
        Version++;
    }

    public void Apply(LandedCostApplied evt)
    {
        LastKnownUnitCost = Normalize(LastKnownUnitCost + evt.TotalLandedCost);
        Version++;
    }

    public void Apply(WrittenDown evt)
    {
        LastKnownUnitCost = Normalize(evt.NewValue);
        Version++;
    }

    private static decimal Normalize(decimal value)
        => decimal.Round(value, Scale, MidpointRounding.AwayFromZero);

    private static DateTime EnsureUtc(DateTime value)
        => value.Kind == DateTimeKind.Utc ? value : DateTime.SpecifyKind(value, DateTimeKind.Utc);
}

/// <summary>Business date of a value event: the calendar date in Europe/Vilnius.</summary>
public static class StockBusinessDate
{
    private static readonly TimeZoneInfo Vilnius = Resolve();

    public static DateTime From(DateTime utc)
    {
        var u = utc.Kind == DateTimeKind.Utc ? utc : DateTime.SpecifyKind(utc, DateTimeKind.Utc);
        return DateTime.SpecifyKind(TimeZoneInfo.ConvertTimeFromUtc(u, Vilnius).Date, DateTimeKind.Unspecified);
    }

    private static TimeZoneInfo Resolve()
    {
        foreach (var id in new[] { "Europe/Vilnius", "FLE Standard Time" })
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
            catch (TimeZoneNotFoundException) { }
            catch (InvalidTimeZoneException) { }
        }

        return TimeZoneInfo.Utc;
    }
}

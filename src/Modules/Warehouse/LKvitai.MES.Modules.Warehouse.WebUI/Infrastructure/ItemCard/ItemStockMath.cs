using System.Globalization;
using LKvitai.MES.Modules.Warehouse.WebUI.Models;

namespace LKvitai.MES.Modules.Warehouse.WebUI.Infrastructure.ItemCard;

/// <summary>Totals of the item's stock rows shown in the header and the Vietos tab.</summary>
public sealed record ItemStockSummary(
    decimal Physical,
    decimal Reserved,
    decimal Available,
    int LocationCount,
    DateTime? LastUpdated)
{
    public static ItemStockSummary From(IReadOnlyCollection<AvailableStockItemDto> rows)
        => new(
            rows.Sum(x => x.PhysicalQty),
            rows.Sum(x => x.ReservedQty),
            rows.Sum(x => x.AvailableQty),
            rows.Select(x => x.Location).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
            rows.Count == 0 ? null : rows.Max(x => x.LastUpdated));
}

/// <summary>Open inbound quantity of the item and the nearest expected date.</summary>
public sealed record ItemIncomingSummary(decimal Total, DateOnly? NearestEta)
{
    public static ItemIncomingSummary From(IReadOnlyCollection<ItemIncomingRowDto> rows)
        => new(
            rows.Sum(x => x.OpenQty),
            rows.Where(x => x.ExpectedDate.HasValue).Select(x => x.ExpectedDate).Min());
}

/// <summary>One coloured part of the header stock bar; <see cref="Percent"/> is of physical + incoming.</summary>
public sealed record StockBarSegment(string Kind, decimal Percent, bool RoundLeft, bool RoundRight)
{
    public string CssWidth => ItemStockMath.CssPercent(Percent);
}

public sealed record StockBar(IReadOnlyList<StockBarSegment> Segments, decimal? MinMarkerPercent)
{
    public string? MinMarkerCss => MinMarkerPercent.HasValue ? ItemStockMath.CssPercent(MinMarkerPercent.Value) : null;
}

public enum MinQtyState
{
    None,
    Above,
    Below
}

/// <summary>Derived values of the item card header: stock rows, bar scale and segments, minimum state and margin.</summary>
public static class ItemStockMath
{
    public const string ReservedSegment = "reserved";
    public const string AvailableSegment = "available";
    public const string IncomingSegment = "incoming";

    /// <summary>Stock role of rows outside the stock boundary (SUPPLIER, CUSTOMER, AGNUM, ...): never item stock.</summary>
    public const string OutsideRole = "Outside";

    public const string ReceivingRole = "Receiving";

    /// <summary>Rows of the item on the territory, ordered by location code (ordinal).</summary>
    public static IReadOnlyList<AvailableStockItemDto> StockRows(IEnumerable<AvailableStockItemDto> rows)
        => rows
            .Where(x => !string.Equals(x.LocationRole, OutsideRole, StringComparison.OrdinalIgnoreCase))
            .OrderBy(x => x.Location, StringComparer.Ordinal)
            .ToList();

    public static bool IsAwaitingPutaway(AvailableStockItemDto row)
        => string.Equals(row.LocationRole, ReceivingRole, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Bar scale = physical + incoming. Segments: reserved, available, incoming (zero-width ones are left out);
    /// the first segment rounds the left edge, the last the right edge. The marker sits at min / scale.
    /// </summary>
    public static StockBar Bar(decimal physical, decimal reserved, decimal available, decimal incoming, decimal? minQty)
    {
        var scale = Math.Max(0m, physical) + Math.Max(0m, incoming);
        if (scale <= 0m)
        {
            return new StockBar(Array.Empty<StockBarSegment>(), null);
        }

        var parts = new[] { (ReservedSegment, reserved), (AvailableSegment, available), (IncomingSegment, incoming) }
            .Where(x => x.Item2 > 0m)
            .ToList();

        var segments = parts
            .Select((part, index) => new StockBarSegment(
                part.Item1,
                ToPercent(part.Item2, scale),
                index == 0,
                index == parts.Count - 1))
            .ToList();

        decimal? marker = minQty.HasValue ? ToPercent(Math.Max(0m, minQty.Value), scale) : null;
        return new StockBar(segments, marker);
    }

    public static MinQtyState MinState(decimal? minQty, decimal available)
        => minQty switch
        {
            null => MinQtyState.None,
            _ when available >= minQty.Value => MinQtyState.Above,
            _ => MinQtyState.Below
        };

    /// <summary>(base − WAC) / base in percent, one decimal; null when either is missing or the base price is not positive.</summary>
    public static decimal? MarginPercent(decimal? basePrice, decimal? unitCost)
    {
        if (basePrice is not > 0m || unitCost is null)
        {
            return null;
        }

        return decimal.Round((basePrice.Value - unitCost.Value) / basePrice.Value * 100m, 1, MidpointRounding.AwayFromZero);
    }

    /// <summary>Whole days from <paramref name="today"/> to <paramref name="eta"/> (negative when overdue).</summary>
    public static int DaysUntil(DateOnly eta, DateOnly today) => eta.DayNumber - today.DayNumber;

    /// <summary>Location for the "Show in 3D" link: the storage row with the most available stock.</summary>
    public static string? PrimaryStorageLocation(IEnumerable<AvailableStockItemDto> rows)
        => rows
            .Where(x => IsStorageRole(x.LocationRole))
            .OrderByDescending(x => x.AvailableQty)
            .ThenBy(x => x.Location, StringComparer.Ordinal)
            .Select(x => x.Location)
            .FirstOrDefault();

    internal static string CssPercent(decimal value)
        => value.ToString("0.##", CultureInfo.InvariantCulture) + "%";

    private static bool IsStorageRole(string? role)
        => role is not null && role is not ("Receiving" or "QcHold" or "Quarantine" or "Shipping" or "Transit" or "Outside");

    private static decimal ToPercent(decimal value, decimal scale)
        => Math.Clamp(decimal.Round(value / scale * 100m, 2, MidpointRounding.AwayFromZero), 0m, 100m);
}

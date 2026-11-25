using System.Globalization;
using LKvitai.MES.Modules.Warehouse.WebUI.Models;

namespace LKvitai.MES.Modules.Warehouse.WebUI.Infrastructure.ItemCard;

public enum MovementKind
{
    Receipt,
    Issue,
    Transfer,
    Adjustment,
    Opening
}

public enum MovementFilter
{
    All,
    Receipts,
    Issues,
    Transfers,
    Adjustments
}

/// <summary>
/// A stock movement of the item with its effect on the item total (<see cref="Delta"/>) and the running item
/// total after it (<see cref="Balance"/>, null when the current total is unknown).
/// </summary>
public sealed record ItemMovement(StockMovementRowDto Source, MovementKind Kind, decimal Delta, decimal? Balance)
{
    /// <summary>"FROM → TO" for moves inside the territory, otherwise the location on the territory.</summary>
    public string Route => ItemMovementTimeline.Route(Source, Delta);
}

/// <summary>
/// Builds the Judėjimai timeline from the stock-movements report: classification, signed quantity and the running
/// item total. The total changes only when a movement crosses the stock boundary (SUPPLIER, CUSTOMER, ADJUSTMENT, ...).
/// </summary>
public static class ItemMovementTimeline
{
    public const string OpeningBalanceReason = "OPENING_BALANCE";
    public const string Arrow = " → ";

    private static readonly HashSet<string> Outside = new(StringComparer.OrdinalIgnoreCase)
    {
        "SUPPLIER", "PRODUCTION", "SCRAP", "CUSTOMER", "RETURN_TO_SUPPLIER", "ADJUSTMENT", "SYSTEM", "AGNUM"
    };

    public static readonly IReadOnlyList<MovementFilter> Filters =
        [MovementFilter.All, MovementFilter.Receipts, MovementFilter.Issues, MovementFilter.Transfers, MovementFilter.Adjustments];

    /// <summary>
    /// Newest first. The balance of the newest movement is <paramref name="currentTotal"/>; each older one is the
    /// newer balance minus the newer movement's delta.
    /// </summary>
    public static IReadOnlyList<ItemMovement> Build(IEnumerable<StockMovementRowDto> movements, decimal? currentTotal)
    {
        var ordered = movements.OrderByDescending(x => x.Timestamp).ToList();
        var result = new List<ItemMovement>(ordered.Count);
        var balance = currentTotal;

        foreach (var movement in ordered)
        {
            var delta = Delta(movement);
            result.Add(new ItemMovement(movement, Classify(movement, delta), delta, balance));
            balance -= delta;
        }

        return result;
    }

    public static decimal Delta(StockMovementRowDto movement)
        => (IsInside(movement.ToLocationCode) ? movement.Qty : 0m) - (IsInside(movement.FromLocationCode) ? movement.Qty : 0m);

    public static MovementKind Classify(StockMovementRowDto movement, decimal delta)
        => movement.MovementType switch
        {
            "Receive" => MovementKind.Receipt,
            "Transfer" => MovementKind.Transfer,
            "Pick" or "Dispatch" => MovementKind.Issue,
            "Adjust" when string.Equals(movement.Reason, OpeningBalanceReason, StringComparison.OrdinalIgnoreCase) => MovementKind.Opening,
            "Adjust" => MovementKind.Adjustment,
            _ => ByDelta(delta)
        };

    public static bool Matches(ItemMovement movement, MovementFilter filter)
        => filter switch
        {
            MovementFilter.Receipts => movement.Kind is MovementKind.Receipt or MovementKind.Opening,
            MovementFilter.Issues => movement.Kind == MovementKind.Issue,
            MovementFilter.Transfers => movement.Kind == MovementKind.Transfer,
            MovementFilter.Adjustments => movement.Kind == MovementKind.Adjustment,
            _ => true
        };

    public static int Count(IEnumerable<ItemMovement> movements, MovementFilter filter)
        => movements.Count(x => Matches(x, filter));

    /// <summary>Chip family of a movement kind: ok / info / slate / warn / done.</summary>
    public static string ChipClass(MovementKind kind)
        => kind switch
        {
            MovementKind.Receipt => "chip chip--ok",
            MovementKind.Issue => "chip chip--reserved",
            MovementKind.Transfer => "chip chip--locked",
            MovementKind.Adjustment => "chip chip--low",
            _ => "chip chip--delivered"
        };

    /// <summary>Dot colour token of a movement kind (rail list).</summary>
    public static string DotClass(MovementKind kind)
        => kind switch
        {
            MovementKind.Receipt => "ic-dot--ok",
            MovementKind.Issue => "ic-dot--info",
            MovementKind.Transfer => "ic-dot--slate",
            MovementKind.Adjustment => "ic-dot--warn",
            _ => "ic-dot--done"
        };

    /// <summary>Quantity colour: ok for increases, danger for decreases, neutral for moves inside the territory.</summary>
    public static string QtyClass(decimal delta)
        => delta switch
        {
            > 0m => "ic-qty--in",
            < 0m => "ic-qty--out",
            _ => "ic-qty--move"
        };

    /// <summary>Signed quantity for increases / decreases; the plain quantity for moves inside the territory.</summary>
    public static string Quantity(ItemMovement movement) => Quantity(movement, CultureInfo.CurrentUICulture);

    public static string Quantity(ItemMovement movement, CultureInfo culture)
        => movement.Delta == 0m
            ? ItemCardFormat.Qty(movement.Source.Qty, culture)
            : ItemCardFormat.Signed(movement.Delta, culture);

    public static string Route(StockMovementRowDto movement, decimal delta)
        => delta switch
        {
            > 0m => movement.ToLocationCode ?? string.Empty,
            < 0m => movement.FromLocationCode ?? string.Empty,
            _ => $"{movement.FromLocationCode}{Arrow}{movement.ToLocationCode}"
        };

    /// <summary>Link of the movement's document, when the WebUI has a page for it (goods receipts: ISH-{id}).</summary>
    public static string? DocumentHref(StockMovementRowDto movement)
    {
        const string receiptPrefix = "ISH-";
        if (movement.MovementType == "Receive"
            && movement.Reference.StartsWith(receiptPrefix, StringComparison.Ordinal)
            && int.TryParse(movement.Reference.AsSpan(receiptPrefix.Length), out var shipmentId))
        {
            return $"inbound/shipments/{shipmentId}";
        }

        return null;
    }

    /// <summary>Document label: GUID references (adjustments) are cut to their first 8 characters.</summary>
    public static string DocumentLabel(StockMovementRowDto movement)
        => Guid.TryParse(movement.Reference, out var id)
            ? id.ToString("N")[..8].ToUpperInvariant()
            : movement.Reference;

    /// <summary>Second line of "Atliko": the reason code of adjustments.</summary>
    public static string? Note(ItemMovement movement)
        => movement.Kind == MovementKind.Adjustment && !string.IsNullOrWhiteSpace(movement.Source.Reason)
            ? movement.Source.Reason
            : null;

    private static bool IsInside(string? code) => !string.IsNullOrWhiteSpace(code) && !Outside.Contains(code.Trim());

    private static MovementKind ByDelta(decimal delta)
        => delta switch
        {
            > 0m => MovementKind.Receipt,
            < 0m => MovementKind.Issue,
            _ => MovementKind.Transfer
        };
}

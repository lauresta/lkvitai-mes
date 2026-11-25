using LKvitai.MES.Modules.Warehouse.WebUI.Models;

namespace LKvitai.MES.Modules.Warehouse.WebUI.Infrastructure.ItemCard;

public enum MoveProblem
{
    None,
    SourceMissing,
    TargetMissing,
    TargetUnknown,
    SameLocation,
    QtyMissing,
    QtyTooHigh
}

/// <summary>One reason of the Koreguoti likutį dialog: the reason code sent to the API and its label.</summary>
public sealed record AdjustReasonOption(string Code, string? LabelKey, string Label);

/// <summary>Validation and option rules of the Perkelti / Koreguoti likutį dialogs.</summary>
public static class StockDialogRules
{
    private static readonly (string LabelKey, string[] Codes)[] PreferredReasons =
    [
        ("AdminItemDetail.AdjustReason.Count", ["INVENTORY", "CYCLE_COUNT"]),
        ("AdminItemDetail.AdjustReason.Damaged", ["DAMAGE", "DAMAGED"]),
        ("AdminItemDetail.AdjustReason.Found", ["FOUND", "INVENTORY"]),
        ("AdminItemDetail.AdjustReason.EntryError", ["DATA_ENTRY", "SYSTEM_ERROR"])
    ];

    /// <summary>
    /// Target ≠ source, a known target, 0 &lt; qty ≤ available. <paramref name="targetId"/> is null while the typed
    /// target has not been resolved; <paramref name="targetText"/> tells an empty field from an unknown code.
    /// </summary>
    public static MoveProblem ValidateMove(int? sourceId, string? targetText, int? targetId, decimal? qty, decimal available)
    {
        if (sourceId is null)
        {
            return MoveProblem.SourceMissing;
        }

        if (string.IsNullOrWhiteSpace(targetText))
        {
            return MoveProblem.TargetMissing;
        }

        if (targetId is null)
        {
            return MoveProblem.TargetUnknown;
        }

        if (targetId == sourceId)
        {
            return MoveProblem.SameLocation;
        }

        return ValidateQty(qty, available);
    }

    public static MoveProblem ValidateQty(decimal? qty, decimal available)
        => qty switch
        {
            null or <= 0m => MoveProblem.QtyMissing,
            _ when qty.Value > available => MoveProblem.QtyTooHigh,
            _ => MoveProblem.None
        };

    /// <summary>Location typed or scanned in "Į vietą": exact code or barcode, case-insensitive.</summary>
    public static AdminLocationDto? MatchLocation(IEnumerable<AdminLocationDto> candidates, string? input)
    {
        var value = input?.Trim();
        if (string.IsNullOrEmpty(value))
        {
            return null;
        }

        return candidates.FirstOrDefault(x => string.Equals(x.Code, value, StringComparison.OrdinalIgnoreCase))
               ?? candidates.FirstOrDefault(x => string.Equals(x.Barcode, value, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Counted − system; null while nothing is counted.</summary>
    public static decimal? Difference(decimal? counted, decimal system)
        => counted.HasValue ? counted.Value - system : null;

    /// <summary>
    /// The four reasons of the design mapped to the first active reason code that exists for each; when none of
    /// them exists, every active code is offered with its own name.
    /// </summary>
    public static IReadOnlyList<AdjustReasonOption> AdjustReasons(IEnumerable<ReasonCodeDto> activeReasons)
    {
        var reasons = activeReasons.Where(x => x.Active).ToList();
        var byCode = reasons
            .GroupBy(x => x.Code.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.First(), StringComparer.OrdinalIgnoreCase);

        var preferred = PreferredReasons
            .Select(x => (x.LabelKey, Code: x.Codes.FirstOrDefault(byCode.ContainsKey)))
            .Where(x => x.Code is not null)
            .Select(x => new AdjustReasonOption(byCode[x.Code!].Code.Trim().ToUpperInvariant(), x.LabelKey, byCode[x.Code!].Name))
            .ToList();

        if (preferred.Count > 0)
        {
            return preferred;
        }

        return reasons
            .OrderBy(x => x.Code, StringComparer.Ordinal)
            .Select(x => new AdjustReasonOption(x.Code.Trim().ToUpperInvariant(), null, string.IsNullOrWhiteSpace(x.Name) ? x.Code : x.Name))
            .ToList();
    }

    /// <summary>Execute right after submit only when the warehouse policy approved the transfer on submit.</summary>
    public static bool CanExecuteAfterSubmit(string? status)
        => string.Equals(status, "APPROVED", StringComparison.OrdinalIgnoreCase);
}

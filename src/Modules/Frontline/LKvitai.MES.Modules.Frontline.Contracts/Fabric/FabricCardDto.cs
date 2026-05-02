namespace LKvitai.MES.Modules.Frontline.Contracts.Fabric;

/// <summary>
/// Full mobile-lookup payload for a single fabric: header (name/photo/notes),
/// per-width stock rows, currently selected width, and suggested alternatives.
/// Aggregates the three result sets of <c>dbo.weblb_Fabric_GetMobileCard</c>
/// into one envelope returned by <c>GET /api/frontline/fabric/{code}</c>.
/// </summary>
/// <remarks>
/// <see cref="SelectedWidthMm"/> is computed server-side: explicit
/// <c>?width=</c> query param if it matches a width row, otherwise the
/// smallest available width with stock (matches legacy controller behaviour).
/// </remarks>
public sealed record FabricCardDto(
    string Code,
    string Name,
    string PhotoUrl,
    string? Notes,
    int? DiscountPercent,
    IReadOnlyList<WidthStockDto> Widths,
    int? SelectedWidthMm,
    IReadOnlyList<FabricAlternativeDto> Alternatives);

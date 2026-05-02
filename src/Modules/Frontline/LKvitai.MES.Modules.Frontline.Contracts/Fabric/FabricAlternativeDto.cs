namespace LKvitai.MES.Modules.Frontline.Contracts.Fabric;

/// <summary>
/// Suggested alternative fabric shown alongside a lookup result. Pre-flattened
/// to a single (Code, Width) pair for fast rendering — the third result set of
/// the legacy <c>dbo.weblb_Fabric_GetMobileCard</c> proc shape.
/// </summary>
public sealed record FabricAlternativeDto(
    string Code,
    string PhotoUrl,
    int WidthMm,
    FabricAvailabilityStatus Status,
    int? StockMeters,
    DateOnly? ExpectedDate);

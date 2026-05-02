namespace LKvitai.MES.Modules.Frontline.Contracts.Fabric;

/// <summary>
/// Stock availability for a specific fabric width (e.g. 2000 mm).
/// </summary>
/// <remarks>
/// Fields <see cref="WidthMm"/>, <see cref="Status"/>, <see cref="StockMeters"/>
/// and <see cref="ExpectedDate"/> map 1:1 to the second result set of the legacy
/// <c>dbo.weblb_Fabric_GetMobileCard</c> proc.
///
/// <para>
/// <b>R-7 (deferred to F-2):</b> <see cref="IncomingMeters"/> and
/// <see cref="IncomingDate"/> are required by the codex spec but absent from the
/// legacy proc result. The F-1 stub fills them from sample data; the F-2 SQL
/// adapter will either extend <c>weblb_Fabric_GetMobileCard</c> or join a
/// supplier-orders table to populate them. Kept on the contract now so the
/// Razor pages don't have to grow a second DTO when F-2 lands.
/// </para>
/// </remarks>
public sealed record WidthStockDto(
    int WidthMm,
    FabricAvailabilityStatus Status,
    int? StockMeters,
    DateOnly? ExpectedDate,
    int? IncomingMeters,
    DateOnly? IncomingDate);

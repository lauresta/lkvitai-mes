namespace LKvitai.MES.Modules.Frontline.Contracts.Fabric;

/// <summary>
/// Single row of the desktop low-stock list: one (fabric, width) pair plus the
/// purchasing context required by the codex desktop spec.
/// </summary>
/// <remarks>
/// <para>
/// <b>R-6 / R-7 (deferred to F-2):</b> the legacy DB has no proc backing this
/// shape. The F-1 stub returns hardcoded sample rows. F-2 will either:
/// </para>
/// <list type="bullet">
///   <item>derive a new view from the legacy <c>web_RemainsAll</c> view (used
///   by the legacy desktop list) and wrap it in a paged proc
///   <c>weblb_Fabric_GetLowStockList(@ThresholdMeters, @Status, @WidthMm,
///   @Supplier, @Page, @PageSize)</c>;</item>
///   <item>create a <c>Frontline_FabricLastChecked</c> table to persist
///   <see cref="LastChecked"/> on every mobile lookup, since no legacy column
///   exists today.</item>
/// </list>
/// <para>
/// <b>Action flags</b> (<see cref="CanReserve"/> / <see cref="CanNotify"/> /
/// <see cref="CanReplace"/>) are projected by the SQL/stub layer so the WebUI
/// renders the correct compact-action button (Reserve / Notify / Replace) per
/// row without re-deriving the rule from <see cref="Status"/>.
/// </para>
/// </remarks>
public sealed record FabricLowStockDto(
    string Code,
    string Name,
    string PhotoUrl,
    int WidthMm,
    int AvailableMeters,
    int ThresholdMeters,
    FabricAvailabilityStatus Status,
    DateOnly? ExpectedDate,
    int? IncomingMeters,
    string? Supplier,
    IReadOnlyList<string> AlternativeCodes,
    DateTimeOffset? LastChecked,
    bool CanReserve,
    bool CanNotify,
    bool CanReplace);

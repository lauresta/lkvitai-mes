using LKvitai.MES.Modules.Frontline.Contracts.Common;
using LKvitai.MES.Modules.Frontline.Contracts.Fabric;

namespace LKvitai.MES.Modules.Frontline.Application.Ports;

/// <summary>
/// Read-side port for fabric-availability queries. Application owns the
/// contract; Infrastructure provides an implementation
/// (<c>StubFabricQueryService</c> in F-1, SQL Server adapter over the legacy
/// <c>weblb_*</c> stored procedures in F-2).
/// </summary>
public interface IFabricQueryService
{
    /// <summary>
    /// Returns the full mobile-lookup card for a single fabric, or <c>null</c>
    /// when no such code exists. Mirrors the legacy
    /// <c>FabricAvailabilityController.Mobile</c> action backed by
    /// <c>dbo.weblb_Fabric_GetMobileCard</c>.
    /// </summary>
    /// <remarks>
    /// Implementations <b>must</b> normalise the code to upper-case and reject
    /// inputs that don't match the legacy regex
    /// <c>^[A-Z0-9\-_./]{2,}$</c>. Validation lives in this layer (not in the
    /// HTTP endpoint) so unit tests for the SQL adapter exercise the same
    /// guard the production controller relies on.
    /// </remarks>
    Task<FabricCardDto?> GetMobileCardAsync(
        FabricLookupParams query,
        CancellationToken cancellationToken);

    /// <summary>
    /// Returns a page of low-stock list rows matching the supplied filters.
    /// Implementations must echo the requested <see cref="FabricLowStockQueryParams.Page"/>
    /// and <see cref="FabricLowStockQueryParams.PageSize"/> on the result envelope so the
    /// UI can render pagination without a separate count call.
    /// </summary>
    Task<PagedResult<FabricLowStockDto>> GetLowStockListAsync(
        FabricLowStockQueryParams query,
        CancellationToken cancellationToken);
}

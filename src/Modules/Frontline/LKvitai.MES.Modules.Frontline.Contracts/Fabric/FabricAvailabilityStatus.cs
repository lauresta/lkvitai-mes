namespace LKvitai.MES.Modules.Frontline.Contracts.Fabric;

/// <summary>
/// Per-width availability status for a fabric. Integer values match the legacy
/// <c>dbo.weblb_Fabric_GetMobileCard</c> stored procedure result columns 1:1 so
/// the F-2 SQL adapter can cast a <c>SqlDataReader</c> int directly to this
/// enum without a translation table.
/// </summary>
/// <remarks>
/// Mapping (legacy SQL → enum):
/// <list type="bullet">
///   <item><c>1</c> → <see cref="Enough"/> (in stock, safe to order)</item>
///   <item><c>2</c> → <see cref="Low"/> (below low threshold but on hand)</item>
///   <item><c>3</c> → <see cref="None"/> (out of stock; ETA may exist)</item>
///   <item><c>4</c> → <see cref="Discontinued"/> (no further deliveries)</item>
///   <item>other → <see cref="Unknown"/></item>
/// </list>
/// </remarks>
public enum FabricAvailabilityStatus
{
    Unknown = 0,
    Enough = 1,
    Low = 2,
    None = 3,
    Discontinued = 4,
}

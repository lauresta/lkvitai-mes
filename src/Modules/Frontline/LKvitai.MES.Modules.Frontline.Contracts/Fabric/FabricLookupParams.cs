namespace LKvitai.MES.Modules.Frontline.Contracts.Fabric;

/// <summary>
/// Query parameters for the mobile fabric lookup. Defaults match the legacy
/// <c>FabricAvailabilityController</c> constants so the F-2 SQL adapter can
/// pass them straight through to <c>dbo.weblb_Fabric_GetMobileCard</c>.
/// </summary>
public sealed record FabricLookupParams(
    string Code,
    int? Width = null,
    int LowThreshold = 10,
    int EnoughThreshold = 25);

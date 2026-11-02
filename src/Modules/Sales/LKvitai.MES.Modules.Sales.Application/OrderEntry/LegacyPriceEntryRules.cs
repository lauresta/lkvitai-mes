namespace LKvitai.MES.Modules.Sales.Application.OrderEntry;

/// <summary>
/// Current Desktop Form_frmNewBlind.mPrice_Enter rule. This describes the type's
/// price input mode only; it must never override a locked product/actor policy.
/// </summary>
public static class LegacyPriceEntryRules
{
    public static string NormalizeMode(string? categoryMode) => categoryMode ?? "any";

    public static bool SupportsManualUnitPriceForType(int typeId, string? categoryMode)
        => !string.Equals(NormalizeMode(categoryMode), "fixed", StringComparison.OrdinalIgnoreCase)
            || typeId is 428417548 or 2112842479;
}

namespace LKvitai.MES.Modules.Sales.Contracts.Orders;

/// <summary>Legacy business marker for the owner-approved privacy notice.
/// This describes presentation only; it does not erase or anonymize stored data.</summary>
public static class LegacyOrderPrivacy
{
    public static bool IsRestricted(string? note) => string.Equals(
        note?.Trim(), "Kliento prasymu anuliuotas", StringComparison.OrdinalIgnoreCase);
}

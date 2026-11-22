namespace LKvitai.MES.Modules.Sales.Contracts.OrderEntry;

/// <summary>
/// Display label for a legacy currency code. <c>dbo.Zinynas_valiutos</c> still lists the pre-euro base currency
/// as LTL with rate 1.0000, and Access keeps writing customer payments with that id, but the amounts are euros.
/// Only what is shown changes; the stored currency id and amount are never touched.
/// </summary>
public static class LegacyCurrencyLabel
{
    public const string LegacyBaseCode = "LTL";
    public const string Euro = "EUR";

    public static string? Display(string? code, decimal? rate) =>
        rate == 1m && string.Equals(code?.Trim(), LegacyBaseCode, StringComparison.OrdinalIgnoreCase) ? Euro : code;
}

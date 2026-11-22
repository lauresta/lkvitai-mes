namespace LKvitai.MES.Modules.Warehouse.WebUI.Infrastructure;

/// <summary>Picks the resource key suffix for a counted noun: One/Other in English, One/Few/Many in Lithuanian.</summary>
public static class PluralForm
{
    public const string One = "One";
    public const string Few = "Few";
    public const string Many = "Many";
    public const string Other = "Other";

    public static string Suffix(int count, string? language)
    {
        var n = Math.Abs(count);
        return string.Equals(language, "lt", StringComparison.OrdinalIgnoreCase) ? Lithuanian(n) : (n == 1 ? One : Other);
    }

    // 1, 21, 31 ... -> one; 2-9, 22-29 ... -> few; 0, 10-20, 30, 40 ... -> many.
    private static string Lithuanian(int n)
    {
        var last = n % 10;
        var lastTwo = n % 100;
        if (last == 1 && lastTwo != 11) return One;
        return last == 0 || lastTwo is >= 11 and <= 19 ? Many : Few;
    }
}

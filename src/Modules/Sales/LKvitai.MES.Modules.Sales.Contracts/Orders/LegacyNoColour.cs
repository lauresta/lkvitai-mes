namespace LKvitai.MES.Modules.Sales.Contracts.Orders;

/// <summary>
/// Legacy products without a chosen material point at component 0, whose code is
/// <c>RSR816</c>. Access prints that code; here it is shown as "no colour".
/// </summary>
public static class LegacyNoColour
{
    public const string Code = "RSR816";

    public static string Display(string? colourCode) =>
        string.Equals(colourCode?.Trim(), Code, StringComparison.OrdinalIgnoreCase)
            ? string.Empty
            : colourCode ?? string.Empty;
}

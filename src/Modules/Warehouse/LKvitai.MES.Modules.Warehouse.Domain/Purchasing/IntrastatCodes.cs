namespace LKvitai.MES.Modules.Warehouse.Domain.Purchasing;

/// <summary>Code lists of the Intrastat arrivals declaration (nature of transaction, mode of transport, delivery terms).</summary>
public static class IntrastatCodes
{
    /// <summary>Transaction code of a return of goods to the supplier.</summary>
    public const string ReturnNature = "21";

    /// <summary>Nature of transaction codes valid since 2022.</summary>
    public static readonly IReadOnlySet<string> NatureOfTransaction = new HashSet<string>(StringComparer.Ordinal)
    {
        "11", "12", "21", "22", "23", "31", "32", "33", "34", "41", "42", "51", "52", "71", "72", "80", "91", "99"
    };

    /// <summary>Mode of transport at the border (single digit).</summary>
    public static readonly IReadOnlySet<string> TransportModes = new HashSet<string>(StringComparer.Ordinal)
    {
        "1", "2", "3", "4", "5", "7", "8", "9"
    };

    /// <summary>Incoterms 2020.</summary>
    public static readonly IReadOnlySet<string> DeliveryTerms = new HashSet<string>(StringComparer.Ordinal)
    {
        "EXW", "FCA", "CPT", "CIP", "DAP", "DPU", "DDP", "FAS", "FOB", "CFR", "CIF"
    };

    public static bool IsNature(string? code) => code is not null && NatureOfTransaction.Contains(code.Trim());

    public static bool IsTransportMode(string? code) => code is not null && TransportModes.Contains(code.Trim());

    public static bool IsDeliveryTerms(string? code)
        => code is not null && DeliveryTerms.Contains(code.Trim().ToUpperInvariant());

    public static bool IsCountryCode(string? code)
        => code is { Length: 2 } && code.All(char.IsAsciiLetter);
}

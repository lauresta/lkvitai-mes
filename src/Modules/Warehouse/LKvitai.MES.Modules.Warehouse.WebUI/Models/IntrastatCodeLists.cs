using System.Globalization;

namespace LKvitai.MES.Modules.Warehouse.WebUI.Models;

/// <summary>Intrastat code lists for the purchase header selects. Same lists as the posting check in the Warehouse domain.</summary>
public static class IntrastatCodeLists
{
    public sealed record CodeOption(string Code, string English, string Lithuanian)
    {
        public string Label
        {
            get
            {
                var text = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "lt" ? Lithuanian : English;
                return string.IsNullOrEmpty(text) ? Code : $"{Code} - {text}";
            }
        }
    }

    /// <summary>Nature of transaction (valid since 2022). Common codes carry a description, the rest show the code only.</summary>
    public static readonly IReadOnlyList<CodeOption> NatureOfTransaction =
    [
        new("11", "Outright purchase", "Pirkimas"),
        new("12", "Supply for sale on approval, consignment or via a commission agent", "Prekių pateikimas patikrinti, komisiniam pardavimui ar per agentą"),
        new("21", "Return of goods", "Prekių grąžinimas"),
        new("22", "Replacement of returned goods", "Grąžintų prekių pakeitimas"),
        new("23", "Replacement of goods that are not returned", "Negrąžinamų prekių pakeitimas"),
        new("31", "", ""), new("32", "", ""), new("33", "", ""), new("34", "", ""),
        new("41", "", ""), new("42", "", ""), new("51", "", ""), new("52", "", ""),
        new("71", "", ""), new("72", "", ""), new("80", "", ""), new("91", "", ""), new("99", "", ""),
    ];

    public static readonly IReadOnlyList<CodeOption> TransportModes =
    [
        new("1", "Sea", "Jūrų"),
        new("2", "Rail", "Geležinkelių"),
        new("3", "Road", "Keliais"),
        new("4", "Air", "Oro"),
        new("5", "Postal consignment", "Pašto siunta"),
        new("7", "Fixed installations (pipeline, power lines)", "Stacionarūs įrenginiai"),
        new("8", "Inland waterway", "Vidaus vandenų"),
        new("9", "Own propulsion", "Savaeigis"),
    ];

    /// <summary>Incoterms 2020.</summary>
    public static readonly IReadOnlyList<CodeOption> DeliveryTerms =
    [
        new("EXW", "Ex Works", "Ex Works"),
        new("FCA", "Free Carrier", "Free Carrier"),
        new("CPT", "Carriage Paid To", "Carriage Paid To"),
        new("CIP", "Carriage and Insurance Paid To", "Carriage and Insurance Paid To"),
        new("DAP", "Delivered at Place", "Delivered at Place"),
        new("DPU", "Delivered at Place Unloaded", "Delivered at Place Unloaded"),
        new("DDP", "Delivered Duty Paid", "Delivered Duty Paid"),
        new("FAS", "Free Alongside Ship", "Free Alongside Ship"),
        new("FOB", "Free On Board", "Free On Board"),
        new("CFR", "Cost and Freight", "Cost and Freight"),
        new("CIF", "Cost, Insurance and Freight", "Cost, Insurance and Freight"),
    ];
}

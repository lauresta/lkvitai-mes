using System.Collections.Frozen;
using System.Text.RegularExpressions;

namespace LKvitai.MES.Modules.Shopfloor.Contracts.Reports;

/// <summary>Allowlisted layout for the Access rptGamDokRoletai dataset. No SQL or expressions.</summary>
public sealed class RoletaiTemplate
{
    public string Report { get; set; } = "rptGamDokRoletai";
    public string Dataset { get; set; } = "dbo.SP_GamRoletaiByTask";
    public string Orientation { get; set; } = "landscape";
    public Dictionary<string, RoletaiSection> Sections { get; set; } = new(StringComparer.Ordinal);

    public static readonly string[] SectionKeys = ["rh", "ch", "uh", "dt", "pf", "pg"];
    public static readonly FrozenSet<string> Fields = new[]
    {
        "Cechas", "Uzsakymas", "UzsakymoData", "GamibinesUzduotiesNumeris",
        "ParduotoGaminioID", "TipoTrPavadinimas", "Valdymas", "Plotis", "Aukstis",
        "kiekis", "Spalva", "Audinys", "Krepianciuju_tipas", "Kreipianciosios",
        "Kasete", "Velenas", "Atsvaras", "Dekoras", "Audinio_sunaudojimas", "Pastabos",
        "Sum(kiekis)", "Page", "Pages", "Now"
    }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    public static RoletaiTemplate Default()
    {
        static RoletaiLine L(string text, decimal size, bool bold = false, bool mono = false,
            string align = "left", bool hide = false) => new() { Text = text, Size = size,
            Bold = bold, Mono = mono, Align = align, HideIfEmpty = hide };
        static RoletaiColumn C(string key, string label, decimal width, bool bold = false,
            bool mono = false, string align = "left") => new() { Key = key, Label = label,
            W = width, Size = 11.5m, Bold = bold, Mono = mono, Align = align };
        return new RoletaiTemplate { Sections = new(StringComparer.Ordinal)
        {
            ["rh"] = new() { Lines = [L("Roletai (bendras kiekis: {Sum(kiekis)} vnt.)", 22, true)] },
            ["ch"] = new() { Lines = [L("Cechas: {Cechas}", 16, true)] },
            ["uh"] = new() { Lines = [L("{Uzsakymas}", 17, true, true),
                L("Užsakymas Nr.: {Uzsakymas} (data - {UzsakymoData}), gam. užd. Nr.: {GamibinesUzduotiesNumeris}", 11)] },
            ["dt"] = new() { Heads = true, Cols = [
                C("TipoTrPavadinimas", "Tipas", 10), C("Valdymas", "Vald.", 10),
                C("Plotis", "Plotis", 5, mono: true, align: "right"),
                C("Aukstis", "Aukštis", 5, mono: true, align: "right"),
                C("kiekis", "Kiekis", 5, mono: true, align: "right"),
                C("Spalva", "Spalva", 7, bold: true), C("Audinys", "Audinys", 12),
                C("Krepianciuju_tipas", "Kreip. tipas", 7),
                C("Kreipianciosios", "Kreipiančiosios", 9), C("Kasete", "Kasetė", 5),
                C("Velenas", "Velenas", 6, mono: true), C("Atsvaras", "Atsvaras", 7),
                C("Dekoras", "Dekoras", 7, bold: true)] },
            ["pf"] = new() { Lines = [L("Audinio sunaudojimas: {Audinio_sunaudojimas}", 10.5m),
                L("Pastabos: {Pastabos}", 10.5m, true, hide: true)] },
            ["pg"] = new() { Lines = [L("Puslapis {Page} iš {Pages}, atspausdinta {Now}", 9.5m,
                mono: true, align: "right")] }
        }};
    }

    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();
        if (Sections is null) return ["Trūksta ataskaitos sekcijų."];
        if (Report != "rptGamDokRoletai" || Dataset != "dbo.SP_GamRoletaiByTask"
            || Orientation != "landscape") errors.Add("Šablono šaltinis arba formatas negalioja.");
        if (Sections.Count != SectionKeys.Length || SectionKeys.Any(key => !Sections.ContainsKey(key)))
            errors.Add("Trūksta privalomos ataskaitos sekcijos.");
        foreach (var (key, section) in Sections)
            ValidateSection(key, section, errors);
        ValidateDetail(Sections.GetValueOrDefault("dt"), errors);
        return errors;
    }

    private static void ValidateSection(string key, RoletaiSection? section, List<string> errors)
    {
        if (!SectionKeys.Contains(key) || section?.Lines is null || section.Cols is null)
        { errors.Add("Nežinoma arba tuščia sekcija."); return; }
        if (section.Lines.Count > 30 || section.Cols.Count > 30)
            errors.Add("Per daug eilučių arba stulpelių.");
        foreach (var line in section.Lines) ValidateLine(line, errors);
        foreach (var col in section.Cols) ValidateColumn(col, errors);
        if (key != "dt" && section.Cols.Count > 0 || key == "dt" && section.Lines.Count > 0)
            errors.Add("Neteisingos sekcijos laukai.");
    }

    private static void ValidateLine(RoletaiLine? line, List<string> errors)
    {
        if (line?.Text is null) { errors.Add("Neteisinga eilutė."); return; }
        if (line.Text.Length > 500 || line.Size is < 7 or > 40 ||
            line.Align is not ("left" or "center" or "right")) errors.Add("Neteisinga eilutė.");
        foreach (Match token in Regex.Matches(line.Text, @"\{([^{}]+)\}", RegexOptions.None,
            TimeSpan.FromMilliseconds(100)))
            if (!Fields.Contains(token.Groups[1].Value)) errors.Add($"Nežinomas laukas: {token.Groups[1].Value}");
    }

    private static void ValidateColumn(RoletaiColumn? col, List<string> errors)
    {
        if (col?.Key is null || col.Label is null || !Fields.Contains(col.Key) || col.Label.Length > 80
            || col.W is < 3 or > 40 || col.Size is < 7 or > 16
            || col.Align is not ("left" or "right")) errors.Add("Neteisingas stulpelis.");
    }

    private static void ValidateDetail(RoletaiSection? detail, List<string> errors)
    {
        if (detail?.Cols is null || !detail.Visible || detail.Cols.Count == 0)
        { errors.Add("Stulpeliai: būtinas bent vienas, plotis iki 100 %, be pasikartojimų."); return; }
        var cols = detail.Cols.Where(c => c is not null).ToArray();
        if (cols.Sum(c => c.W) > 100 || cols.Select(c => c.Key).Distinct(StringComparer.OrdinalIgnoreCase).Count() != detail.Cols.Count)
            errors.Add("Stulpeliai: būtinas bent vienas, plotis iki 100 %, be pasikartojimų.");
    }
}

public sealed class RoletaiSection
{
    public bool Visible { get; set; } = true;
    public bool Heads { get; set; }
    public List<RoletaiLine> Lines { get; set; } = [];
    public List<RoletaiColumn> Cols { get; set; } = [];
}

public sealed class RoletaiLine
{
    public string Text { get; set; } = "";
    public decimal Size { get; set; } = 11;
    public bool Bold { get; set; }
    public bool Mono { get; set; }
    public string Align { get; set; } = "left";
    public bool HideIfEmpty { get; set; }
}

public sealed class RoletaiColumn
{
    public string Key { get; set; } = "";
    public string Label { get; set; } = "";
    public decimal W { get; set; } = 6;
    public decimal Size { get; set; } = 11.5m;
    public bool Bold { get; set; }
    public bool Mono { get; set; }
    public string Align { get; set; } = "left";
}

public sealed record RoletaiTemplateVersion(int Version, DateTimeOffset PublishedAt, string PublishedBy);
public sealed record RoletaiTemplateState(RoletaiTemplate Draft, RoletaiTemplate Published,
    int PublishedVersion, IReadOnlyList<RoletaiTemplateVersion> Versions, bool CanEdit);

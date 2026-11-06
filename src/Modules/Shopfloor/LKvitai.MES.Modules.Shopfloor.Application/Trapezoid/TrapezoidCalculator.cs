using System.Text.Json;

namespace LKvitai.MES.Modules.Shopfloor.Application.Trapezoid;

public sealed record TrapezoidInputs(string ControlType, double WidthMm, double ShortHeightMm,
    double TallHeightMm, int SlatWidthMm, double OverhangMm);

public sealed record TrapezoidSlat(int Number, double FinishedLengthRawMm, double CutLengthRawMm,
    double CutLengthDisplayMm, double AlongRailMm, double HorizontalMm);

public sealed record LegacyTrapnewProfile(double MountingMm = 5, double ProfileMm = 25,
    double HookMm = 16, double ControlMm = 15, double MaxSpacerCutMm = 130,
    double SpacerAllowanceMm = 6, double RailAllowanceMm = 0,
    double FixedMechanismOffsetMm = 5.5, double SlatCutAllowanceMm = 45,
    double FabricExtraPerSlatMm = 27);

public sealed record TrapezoidResult(double Angle, double DiagonalMm, double RailBlankMm,
    double RailCutMm, double FixedOffsetMm, double FreeOffsetMm, double FixedToMechanismMm,
    double SpanMm, double LookupWidthMm, int BaseCount, int ExtraCount, int SlatCount,
    double PitchMm, double SpacerCutMm, double ExtenderMm, double FirstFinishedMm,
    double IncrementMm, double CutTotalRawMm, double FabricUsagePerProductRawM,
    IReadOnlyList<TrapezoidSlat> Slats, IReadOnlyList<string> Notes);

public sealed class TrapezoidCalculationException(string code, string? detail = null)
    : Exception(detail ?? code)
{
    public string Code { get; } = code;
}

/// <summary>Faithful port of trapnew.xls reference-calculator.mjs, profile legacy-trapnew-v1.</summary>
public static class TrapezoidCalculator
{
    public const string ProfileVersion = "legacy-trapnew-v1";
    public const string AlgorithmVersion = "1";
    public static LegacyTrapnewProfile DefaultProfile { get; } = new();

    private sealed record Band(double Key, double Value);
    private sealed record Table(double MaxKey, Band[] Bands);
    private sealed record Tables(string SourceSha256, Table Narrow, Table Wide, Table Extender);
    private static readonly Lazy<Tables> Reference = new(LoadTables);
    public static string LookupTableVersion => Reference.Value.SourceSha256;

    public static double ExcelRound(double value, int digits = 0)
    {
        var factor = Math.Pow(10, digits);
        return Math.Sign(value) * Math.Floor(Math.Abs(value) * factor + 0.5) / factor;
    }

    public static TrapezoidResult Calculate(TrapezoidInputs input, LegacyTrapnewProfile? profile = null)
    {
        var p = profile ?? DefaultProfile;
        var w = input.WidthMm;
        var hs = input.ShortHeightMm;
        var ht = input.TallHeightMm;
        var b = input.SlatWidthMm;
        var o = input.OverhangMm;
        var type = input.ControlType?.Trim().ToUpperInvariant();
        if (!new[] { w, hs, ht, o }.All(double.IsFinite) || w <= 0 || hs <= 0 || ht < hs || o < 0)
            throw new TrapezoidCalculationException("InvalidDimensions");
        if (type is not ("A" or "B") || b is not (89 or 127))
            throw new TrapezoidCalculationException("UnsupportedConfiguration");

        if (p.MaxSpacerCutMm <= p.SpacerAllowanceMm)
            throw new TrapezoidCalculationException("InvalidTechnologyProfile");
        var h1 = p.MountingMm + p.ProfileMm;
        var angle = Math.Atan2(ht - hs, w);
        var cos = Math.Cos(angle);
        var sin = Math.Sin(angle);
        var tan = Math.Tan(angle);
        var diagonalMm = Math.Sqrt(w * w + (ht - hs) * (ht - hs));
        var railBlankMm = diagonalMm - h1 * tan - p.RailAllowanceMm;
        var railCutMm = railBlankMm - p.ControlMm;
        var fixedOffsetMm = (b / 2.0 - (type == "A" ? o : 0)) / cos + p.HookMm * tan;
        var freeOffsetMm = b / (2.0 * cos) - (type == "A" ? p.HookMm : h1 + p.HookMm) * tan;
        var spanMm = railCutMm - fixedOffsetMm - freeOffsetMm;
        var lookupWidthMm = ExcelRound(w - h1 * sin - (p.ControlMm + p.RailAllowanceMm) * cos);
        var tables = Reference.Value;
        var table = b == 89 ? tables.Narrow : tables.Wide;
        var baseCount = (int)Lookup(table, lookupWidthMm);
        if (baseCount < 2 || spanMm <= 0)
            throw new TrapezoidCalculationException("InvalidCarrierSpan");
        var basePitchMm = spanMm / (baseCount - 1);
        var extraCount = basePitchMm > p.MaxSpacerCutMm - p.SpacerAllowanceMm
            ? Math.Max(0, (int)Math.Ceiling(spanMm / (p.MaxSpacerCutMm - p.SpacerAllowanceMm) + 1) - baseCount)
            : 0;
        // The 89 mm workbook LOOKUP shifts both the lookup key and output; preserve it.
        var slatCount = b == 89
            ? (int)Lookup(table, lookupWidthMm - extraCount) + 2 * extraCount
            : baseCount + extraCount;
        if (slatCount < 2) throw new TrapezoidCalculationException("InvalidSlatCount");
        if (slatCount > 78) throw new TrapezoidCalculationException("BeyondLegacyPrintCapacity");
        var pitchMm = spanMm / (slatCount - 1);
        var firstSlatX = fixedOffsetMm * cos;
        var lastSlatX = (fixedOffsetMm + spanMm) * cos;
        if (firstSlatX < 0 || lastSlatX > w)
            throw new TrapezoidCalculationException("SlatsOutsideOpening");
        var desiredExtenderMm = b / 2.0 * tan - p.HookMm / cos;
        var extenderMm = desiredExtenderMm < 0 ? 0 : Lookup(tables.Extender, ExcelRound(desiredExtenderMm));
        if (!double.IsFinite(extenderMm))
            throw new TrapezoidCalculationException("UnsupportedExtender");
        var firstFinishedMm = type == "A"
            ? hs - h1 * cos + p.ControlMm * sin + b / 2.0 * tan - p.HookMm / cos - extenderMm
            : hs - h1 / cos + b / 2.0 * tan - p.HookMm / cos - extenderMm;
        var incrementMm = pitchMm * sin;
        var slats = Enumerable.Range(0, slatCount).Select(i =>
        {
            var finishedMm = firstFinishedMm + i * incrementMm;
            var cutMm = finishedMm + p.SlatCutAllowanceMm;
            return new TrapezoidSlat(i + 1, finishedMm, cutMm, ExcelRound(cutMm),
                i * pitchMm, i * pitchMm * cos);
        }).ToArray();
        if (railCutMm <= 0 || slats.Any(s => s.FinishedLengthRawMm <= 0))
            throw new TrapezoidCalculationException("NonPositiveResult");
        var cutTotalMm = slats.Sum(s => s.CutLengthRawMm);
        var fabricM = (cutTotalMm + slatCount * p.FabricExtraPerSlatMm) / 1000;
        var notes = new List<string>();
        if (type == "B" && o != 0) notes.Add("OverhangUnusedByLegacyB");
        if (b == 89 && extraCount > 0) notes.Add("Legacy89ShiftedLookup");
        return new TrapezoidResult(angle, diagonalMm, railBlankMm, railCutMm, fixedOffsetMm,
            freeOffsetMm, fixedOffsetMm - p.FixedMechanismOffsetMm, spanMm, lookupWidthMm,
            baseCount, extraCount, slatCount, pitchMm, pitchMm + p.SpacerAllowanceMm,
            extenderMm, firstFinishedMm, incrementMm, cutTotalMm, fabricM, slats, notes);
    }

    private static double Lookup(Table table, double key)
    {
        if (!double.IsFinite(key) || key < table.Bands[0].Key || key > table.MaxKey)
            throw new TrapezoidCalculationException("OutsideReferenceTable", key.ToString("G17"));
        var lo = 0;
        var hi = table.Bands.Length;
        while (lo + 1 < hi)
        {
            var mid = (lo + hi) / 2;
            if (table.Bands[mid].Key <= key) lo = mid;
            else hi = mid;
        }
        return table.Bands[lo].Value;
    }

    private static Tables LoadTables()
    {
        var assembly = typeof(TrapezoidCalculator).Assembly;
        var resource = assembly.GetManifestResourceNames().Single(name => name.EndsWith("Trapezoid.legacy-tables.json", StringComparison.Ordinal));
        using var stream = assembly.GetManifestResourceStream(resource)!;
        using var document = JsonDocument.Parse(stream);
        var root = document.RootElement;
        static Table Parse(JsonElement element) => new(
            element.GetProperty("maxKey").GetDouble(),
            element.GetProperty("bands").EnumerateArray().Select(band =>
                new Band(band[0].GetDouble(), band[1].ValueKind == JsonValueKind.Number
                    ? band[1].GetDouble() : double.NaN)).ToArray());
        return new Tables(root.GetProperty("sourceSha256").GetString()!, Parse(root.GetProperty("89")),
            Parse(root.GetProperty("127")), Parse(root.GetProperty("extender")));
    }
}

using System.Globalization;
using System.Resources;
using LKvitai.MES.Modules.Shopfloor.Application.Trapezoid;
using PdfSharp.Drawing;
using PdfSharp.Fonts;
using PdfSharp.Pdf;

namespace LKvitai.MES.Modules.Shopfloor.Api.Endpoints;

/// <summary>Vector A4 landscape output from an immutable saved calculation; no SQL reads.</summary>
public static class TrapezoidPdfRenderer
{
    private static readonly object FontLock = new();
    private static bool _fontReady;
    private static readonly XBrush Ink = XBrushes.Black;
    private static readonly XPen Hair = new(XColor.FromArgb(190, 194, 198), .5);
    private static readonly XPen Dark = new(XColor.FromArgb(25, 28, 33), 1.3);
    private static readonly XBrush Grey = new XSolidBrush(XColor.FromArgb(95, 99, 105));
    private static readonly XBrush Teal = new XSolidBrush(XColor.FromArgb(32, 105, 103));
    private static readonly XBrush Highlight = new XSolidBrush(XColor.FromArgb(232, 246, 245));
    private static readonly ResourceManager Labels = new(
        "LKvitai.MES.Modules.Shopfloor.Api.Endpoints.TrapezoidPdfResources",
        typeof(TrapezoidPdfRenderer).Assembly);
    private static string Label(TrapezoidVersionSnapshot s, string key, params object[] args) =>
        string.Format(CultureInfo.GetCultureInfo(s.Language),
            Labels.GetString(key, CultureInfo.GetCultureInfo(s.Language)) ?? key, args);

    public static byte[] Render(TrapezoidVersionSnapshot snapshot)
    {
        EnsureFonts();
        var result = snapshot.Calculation.Result;
        using var document = new PdfDocument();
        document.Info.Title = $"Vertikalios · Trapecija · {snapshot.Item.OrderNumber} · v{snapshot.Version}";
        document.Info.Subject = "Saved production calculation";
        var pages = 1 + (int)Math.Ceiling(Math.Max(0, result.SlatCount - 30) / 60.0);
        for (var pageIndex = 0; pageIndex < pages; pageIndex++)
        {
            var page = document.AddPage();
            page.Size = PdfSharp.PageSize.A4;
            page.Orientation = PdfSharp.PageOrientation.Landscape;
            using var g = XGraphics.FromPdfPage(page);
            DrawHeader(g, snapshot, pageIndex > 0);
            if (pageIndex == 0)
            {
                DrawDimensions(g, snapshot);
                DrawScheme(g, snapshot);
                DrawCutList(g, snapshot, 0,
                    Math.Min(15, (result.SlatCount + 1) / 2), 2, 462, 139, 165);
                DrawSummary(g, snapshot);
            }
            else DrawCutList(g, snapshot, 30 + (pageIndex - 1) * 60, 20, 3, 38, 120, 249);
            DrawFooter(g, snapshot, pageIndex + 1, pages);
        }
        using var stream = new MemoryStream();
        document.Save(stream, false);
        return stream.ToArray();
    }

    private static void EnsureFonts()
    {
        if (_fontReady) return;
        lock (FontLock)
        {
            if (_fontReady) return;
            GlobalFontSettings.FontResolver = new DejaVuResolver();
            _fontReady = true;
        }
    }

    private static XFont Font(double size, bool bold = false) =>
        new("DejaVu", size, bold ? XFontStyleEx.Bold : XFontStyleEx.Regular);
    private static void Text(XGraphics g, string value, double x, double y,
        double size = 9, bool bold = false, XBrush? brush = null)
        => g.DrawString(value, Font(size, bold), brush ?? Ink, new XPoint(x, y));
    private static string N(double value, string format = "0.0") => value.ToString(format, CultureInfo.InvariantCulture);
    private static string Mark(TrapezoidVersionSnapshot s, string field) =>
        s.Sources.GetValueOrDefault(field) == "manual" ? "*" : "";

    private static void DrawHeader(XGraphics g, TrapezoidVersionSnapshot s, bool continued)
    {
        Text(g, "Vertikalios · Trapecija" + (continued ? " · " + Label(s,"continued") : ""), 38, 49, 18, true);
        Text(g, $"{Label(s,"productionSheet")} · {s.Item.MaterialCode} · {s.Item.MaterialName} · {s.Inputs.ControlType} · {s.Inputs.SlatWidthMm} mm", 38, 69, 9);
        Text(g, $"{s.Item.OrderNumber} · pos. {s.Item.Position}", 620, 43, 10, true);
        Text(g, $"{s.TaskNumber} · {s.Item.Barcode}", 620, 57, 8);
        Text(g, $"{s.SavedAt:yyyy-MM-dd} · {s.SavedBy} · v{s.Version}", 620, 70, 8);
        g.DrawLine(Dark, 38, 79, 804, 79);
    }

    private static void DrawDimensions(XGraphics g, TrapezoidVersionSnapshot s)
    {
        var i = s.Inputs;
        var line = $"{Label(s,"d_type")} {i.ControlType}{Mark(s,"controlType")}    {Label(s,"d_slat")} {i.SlatWidthMm}{Mark(s,"slatWidthMm")} mm    AD {N(i.WidthMm!.Value, "0")}{Mark(s,"widthMm")}    AB {N(i.ShortHeightMm!.Value, "0")}{Mark(s,"shortHeightMm")}    CD {N(i.TallHeightMm!.Value, "0")}{Mark(s,"tallHeightMm")}    {Label(s,"d_o")} {N(i.OverhangMm!.Value, "0")}{Mark(s,"overhangMm")}    {Label(s,"d_side")} {i.HighSide}{Mark(s,"highSide")}    {Label(s,"d_qty")} {s.Item.Quantity}";
        Text(g, line, 38, 97, 8);
        g.DrawLine(Hair, 38, 104, 804, 104);
        var manual = s.Sources.Where(x => x.Value == "manual").Select(x => x.Key switch
        {
            "widthMm" => "AD", "shortHeightMm" => "AB", "tallHeightMm" => "CD",
            "overhangMm" => Label(s,"d_o"), "highSide" => Label(s,"d_side"),
            "slatWidthMm" => Label(s,"d_slat"), "controlType" => Label(s,"d_type"), _ => x.Key
        });
        if (manual.Any()) Text(g, $"* {Label(s,"src_manual")}: {string.Join(", ", manual)}", 38, 116, 7, false, Grey);
    }

    private static void DrawScheme(XGraphics g, TrapezoidVersionSnapshot s)
    {
        var i = s.Inputs;
        var r = s.Calculation.Result;
        var w = i.WidthMm!.Value;
        var ht = i.TallHeightMm!.Value;
        var hs = i.ShortHeightMm!.Value;
        var scale = Math.Min(375 / w, 247 / ht);
        var left = 62.0;
        var bottom = 410.0;
        var pxWidth = w * scale;
        var leftHeight = i.HighSide == "Left" ? ht : hs;
        var rightHeight = i.HighSide == "Left" ? hs : ht;
        g.DrawLine(Hair, left, bottom, left, bottom - leftHeight * scale);
        g.DrawLine(Hair, left, bottom - leftHeight * scale, left + pxWidth, bottom - rightHeight * scale);
        g.DrawLine(Hair, left + pxWidth, bottom - rightHeight * scale, left + pxWidth, bottom);
        g.DrawLine(Hair, left, bottom, left + pxWidth, bottom);
        g.DrawLine(new XPen(XColors.Black, 2.4), left,
            bottom - leftHeight * scale + 5, left + pxWidth,
            bottom - rightHeight * scale + 5);
        foreach (var slat in r.Slats)
        {
            var fromFixed = (r.FixedOffsetMm + slat.AlongRailMm) * Math.Cos(r.Angle) * scale;
            var x = i.HighSide == "Left" ? left + pxWidth - fromFixed : left + fromFixed;
            var height = slat.FinishedLengthRawMm * scale;
            g.DrawRectangle(Hair, x - 1, bottom - height, 2, height);
            if (slat.Number is 1 || slat.Number == r.SlatCount || slat.Number % Math.Max(1, r.SlatCount / 13) == 0)
                Text(g, slat.Number.ToString(CultureInfo.InvariantCulture), x - 2, bottom + 10, 8);
        }
        Text(g, $"{(i.HighSide == "Left" ? "CD" : "AB")} {N(leftHeight)}",
            left - 22, bottom - leftHeight * scale / 2, 9);
        Text(g, $"{(i.HighSide == "Left" ? "AB" : "CD")} {N(rightHeight)}",
            left + pxWidth + 2, bottom - rightHeight * scale / 2, 9);
        Text(g, $"AD {N(w)}", left + pxWidth / 2 - 20, bottom + 24, 9, true);
        Text(g, $"BC {N(r.DiagonalMm)} · {Label(s,"railLbl")} {N(r.RailCutMm)}", left, 137, 9);
        Text(g, Label(s,"schemeNote",r.SlatCount), left, 451, 7, false, Grey);
    }

    private static void DrawCutList(XGraphics g, TrapezoidVersionSnapshot s,
        int start, int rowsPerColumn, int columns, double x, double y, double width)
    {
        Text(g, Label(s,"cutList"), x, y, 11, true);
        Text(g, Label(s,"cutRule"), x, y + 12, 7, false, Grey);
        var slats = s.Calculation.Result.Slats.Skip(start).Take(rowsPerColumn * columns).ToArray();
        for (var column = 0; column < columns; column++)
        {
            var cx = x + column * (width + 7);
            var cy = y + 30;
            Text(g, Label(s,"th_no"), cx, cy, 7, true);
            Text(g, Label(s,"th_fin"), cx + 31, cy, 7, true);
            Text(g, Label(s,"th_cut"), cx + 84, cy, 7, true);
            Text(g, Label(s,"th_qty"), cx + width - 25, cy, 7, true);
            g.DrawLine(Dark, cx, cy + 3, cx + width, cy + 3);
            foreach (var slat in slats.Skip(column * rowsPerColumn).Take(rowsPerColumn))
            {
                cy += 16;
                Text(g, $"n{slat.Number}", cx, cy, 8, true);
                Text(g, N(slat.FinishedLengthRawMm), cx + 31, cy, 8, false, Grey);
                Text(g, N(slat.CutLengthDisplayMm, "0"), cx + 84, cy, 8, true);
                Text(g, N((double)s.Item.Quantity, "0.###"), cx + width - 25, cy, 8);
                g.DrawLine(Hair, cx, cy + 3, cx + width, cy + 3);
            }
        }
    }

    private static void DrawSummary(XGraphics g, TrapezoidVersionSnapshot s)
    {
        var r = s.Calculation.Result;
        var x = 62.0;
        var y = 469.0;
        var labels = new[] { "s_bc", "s_rail", "s_fix", "s_pitch", "s_ext", "s_slats" }
            .Select(key => Label(s,key)).ToArray();
        var values = new[] { N(r.DiagonalMm) + " mm", N(r.RailCutMm) + " mm",
            N(r.FixedOffsetMm) + " / " + N(r.FixedToMechanismMm),
            N(r.PitchMm) + " / " + N(r.SpacerCutMm), N(r.ExtenderMm) + " mm",
            r.SlatCount.ToString(CultureInfo.InvariantCulture) };
        for (var i = 0; i < 6; i++)
        {
            var cx = x + i % 3 * 125;
            var cy = y + i / 3 * 28;
            g.DrawRectangle(Hair, cx, cy, 125, 28);
            Text(g, labels[i], cx + 5, cy + 10, 6, false, Grey);
            Text(g, values[i], cx + 5, cy + 22, 9, true);
        }
        Text(g, $"{Label(s,"tot_items")} {s.Item.Quantity}  ·  {Label(s,"tot_ss")} {r.SlatCount}  ·  {Label(s,"tot_s")} {r.SlatCount * s.Item.Quantity}  ·  {Label(s,"tot_fs")} {N(r.FabricUsagePerProductRawM, "0.000")} m  ·  {Label(s,"tot_f")} {N(r.FabricUsagePerProductRawM * (double)s.Item.Quantity, "0.000")} m", 38, 542, 8, true);
        Text(g, Label(s,"footnote"), 38, 554, 7, false, Grey);
    }

    private static void DrawFooter(XGraphics g, TrapezoidVersionSnapshot s, int page, int count)
    {
        g.DrawLine(Hair, 38, 569, 804, 569);
        Text(g, $"{s.Calculation.ProfileVersion} · calc v{s.Calculation.AlgorithmVersion} · trapnew.xls", 38, 582, 7, false, Grey);
        Text(g, Label(s,"pageF",page,count), 747, 582, 7, false, Grey);
    }

    private sealed class DejaVuResolver : IFontResolver
    {
        private const string LinuxDir = "/usr/share/fonts/truetype/dejavu";
        public FontResolverInfo? ResolveTypeface(string familyName, bool isBold, bool isItalic) =>
            new(isBold ? "DejaVuBold" : "DejaVuRegular");
        public byte[]? GetFont(string faceName)
        {
            var name = faceName == "DejaVuBold" ? "DejaVuSans-Bold.ttf" : "DejaVuSans.ttf";
            var path = Path.Combine(LinuxDir, name);
            if (!File.Exists(path))
                path = Path.Combine("/System/Library/Fonts/Supplemental",
                    faceName == "DejaVuBold" ? "Arial Bold.ttf" : "Arial.ttf");
            return File.Exists(path) ? File.ReadAllBytes(path) : null;
        }
    }
}

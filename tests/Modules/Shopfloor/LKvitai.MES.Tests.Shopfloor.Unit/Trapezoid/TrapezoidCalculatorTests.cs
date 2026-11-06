using System.Text.Json;
using LKvitai.MES.Modules.Shopfloor.Application.Trapezoid;
using Xunit;

namespace LKvitai.MES.Tests.Shopfloor.Unit.Trapezoid;

public sealed class TrapezoidCalculatorTests
{
    [Fact]
    public void All_200_reference_cuts_match_the_saved_workbook_and_recalculations()
    {
        var assembly = typeof(TrapezoidCalculatorTests).Assembly;
        var name = assembly.GetManifestResourceNames().Single(x => x.EndsWith("Trapezoid.fixtures.json", StringComparison.Ordinal));
        using var stream = assembly.GetManifestResourceStream(name)!;
        using var json = JsonDocument.Parse(stream);
        var compared = 0;
        foreach (var fixture in json.RootElement.GetProperty("cases").EnumerateArray())
        {
            var source = fixture.GetProperty("input");
            var expected = fixture.GetProperty("expected");
            var profile = fixture.TryGetProperty("profileOverride", out var profileOverride)
                && profileOverride.TryGetProperty("maxSpacerCutMm", out var maxSpacer)
                ? TrapezoidCalculator.DefaultProfile with { MaxSpacerCutMm = maxSpacer.GetDouble() }
                : TrapezoidCalculator.DefaultProfile;
            var result = TrapezoidCalculator.Calculate(new TrapezoidInputs(
                source.GetProperty("controlType").GetString()!,
                source.GetProperty("widthMm").GetDouble(),
                source.GetProperty("shortHeightMm").GetDouble(),
                source.GetProperty("tallHeightMm").GetDouble(),
                source.GetProperty("slatWidthMm").GetInt32(),
                source.GetProperty("overhangMm").GetDouble()), profile);
            var label = fixture.GetProperty("name").GetString();
            Near(result.RailBlankMm, expected.GetProperty("railBlankMm").GetDouble(), label!);
            Near(result.RailCutMm, expected.GetProperty("railCutMm").GetDouble(), label!);
            Near(result.FixedOffsetMm, expected.GetProperty("fixedOffsetMm").GetDouble(), label!);
            Near(result.FreeOffsetMm, expected.GetProperty("freeOffsetMm").GetDouble(), label!);
            Near(result.LookupWidthMm, expected.GetProperty("lookupWidthMm").GetDouble(), label!);
            Near(result.PitchMm, expected.GetProperty("pitchMm").GetDouble(), label!);
            Near(result.ExtenderMm, expected.GetProperty("extenderMm").GetDouble(), label!);
            Near(result.FirstFinishedMm, expected.GetProperty("firstFinishedMm").GetDouble(), label!);
            Near(result.IncrementMm, expected.GetProperty("incrementMm").GetDouble(), label!);
            Near(result.FabricUsagePerProductRawM, expected.GetProperty("fabricM").GetDouble(), label!);
            Assert.Equal((int)expected.GetProperty("slatCount").GetDouble(), result.SlatCount);
            if (expected.TryGetProperty("extraCount", out var extra)) Assert.Equal((int)extra.GetDouble(), result.ExtraCount);
            var cuts = expected.GetProperty("cutsMm").EnumerateArray().ToArray();
            Assert.Equal(cuts.Length, result.Slats.Count);
            for (var i = 0; i < cuts.Length; i++)
            {
                Near(result.Slats[i].CutLengthRawMm, cuts[i].GetDouble(), $"{label} cut {i + 1}");
                compared++;
            }
        }
        Assert.Equal(200, compared);
    }

    [Theory]
    [InlineData("A", 0, 1600, 1800, 89, 0, "InvalidDimensions")]
    [InlineData("A", 2000, 1801, 1800, 89, 0, "InvalidDimensions")]
    [InlineData("A", 2000, 1600, 1800, 100, 0, "UnsupportedConfiguration")]
    [InlineData("C", 2000, 1600, 1800, 89, 0, "UnsupportedConfiguration")]
    [InlineData("A", 7000, 1600, 1800, 89, 0, "OutsideReferenceTable")]
    public void Invalid_or_unverified_inputs_have_typed_errors(string type, double width,
        double shortHeight, double tallHeight, int slat, double overhang, string code)
    {
        var error = Assert.Throws<TrapezoidCalculationException>(() => TrapezoidCalculator.Calculate(
            new TrapezoidInputs(type, width, shortHeight, tallHeight, slat, overhang)));
        Assert.Equal(code, error.Code);
    }

    [Fact]
    public void Excel_rounding_is_away_from_zero_at_half()
    {
        Assert.Equal(12, TrapezoidCalculator.ExcelRound(11.5));
        Assert.Equal(-12, TrapezoidCalculator.ExcelRound(-11.5));
    }

    private static void Near(double actual, double expected, string label) =>
        Assert.True(Math.Abs(actual - expected) < 1e-7, $"{label}: expected {expected:G17}, actual {actual:G17}");
}

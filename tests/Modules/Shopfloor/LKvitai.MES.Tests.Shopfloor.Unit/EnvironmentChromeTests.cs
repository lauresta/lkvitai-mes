using System.Globalization;
using FluentAssertions;
using LKvitai.MES.BuildingBlocks.WebUI.Localization;
using LKvitai.MES.BuildingBlocks.WebUI.Services;
using LKvitai.MES.Modules.Shopfloor.WebUI.Resources;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace LKvitai.MES.Tests.Shopfloor.Unit;

/// <summary>Production never shows the "not production data" strip, scaffold samples or infrastructure diagnostics.</summary>
public class EnvironmentChromeTests
{
    private static readonly ResourceManagerStringLocalizerFactory Factory =
        new(Options.Create(new LocalizationOptions()), NullLoggerFactory.Instance);

    [Theory]
    [InlineData("Production", false)]
    [InlineData("production", false)]
    [InlineData(" Production ", false)]
    [InlineData("Test", true)]
    [InlineData("Staging", true)]
    [InlineData("Development", true)]
    [InlineData(null, true)]
    public void Environment_strip_is_hidden_only_in_production(string? environment, bool expected)
        => EnvironmentChrome.ShowEnvironmentStrip(environment).Should().Be(expected);

    [Theory]
    [InlineData("Development", true)]
    [InlineData("Test", true)]
    [InlineData("test", true)]
    [InlineData("Production", false)]
    [InlineData("Staging", false)]
    [InlineData(null, false)]
    public void Scaffold_and_diagnostics_are_limited_to_development_and_test(string? environment, bool expected)
        => EnvironmentChrome.ShowDiagnostics(environment).Should().Be(expected);

    [Theory]
    [InlineData("en")]
    [InlineData("lt")]
    public void Production_dashboard_links_and_scanning_notice_have_text_in_both_languages(string culture)
    {
        using var _ = new CultureScope(culture);
        var shopfloor = new StringLocalizer<ShopfloorResources>(Factory);
        var shell = new StringLocalizer<ShellResources>(Factory);
        var keys = new[] { "Eyebrow", "Title", "Lead", "Gamyba.Title", "Gamyba.Description", "Roletai.Title", "Roletai.Description",
            "Queue.Title", "Queue.Description", "Workflows.Title", "Workflows.Description" };

        foreach (var key in keys) shopfloor["Shopfloor.Live." + key].ResourceNotFound.Should().BeFalse(key);
        shell["Scanning.InDevelopment.Title"].ResourceNotFound.Should().BeFalse();
        shell["Scanning.InDevelopment.Body"].ResourceNotFound.Should().BeFalse();
    }

    [Fact]
    public void Lithuanian_scanning_notice_differs_from_english()
    {
        var shell = new StringLocalizer<ShellResources>(Factory);
        string Title(string culture)
        {
            using var _ = new CultureScope(culture);
            return shell["Scanning.InDevelopment.Title"].Value;
        }

        Title("lt").Should().NotBe(Title("en"));
    }

    private sealed class CultureScope : IDisposable
    {
        private readonly CultureInfo _previous = CultureInfo.CurrentUICulture;

        public CultureScope(string culture) => CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture);

        public void Dispose() => CultureInfo.CurrentUICulture = _previous;
    }
}

using System.Globalization;
using FluentAssertions;
using LKvitai.MES.BuildingBlocks.Search;
using LKvitai.MES.Modules.Shopfloor.WebUI.Resources;
using LKvitai.MES.Modules.Shopfloor.WebUI.Shared;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace LKvitai.MES.Tests.Shopfloor.Unit;

/// <summary>Portal "Search everything" (issue #375): the Shopfloor sidebar definition as search screens.</summary>
public class ShopfloorNavigationSearchTests
{
    private static readonly IStringLocalizer<ShopfloorResources> Text = new StringLocalizer<ShopfloorResources>(
        new ResourceManagerStringLocalizerFactory(Options.Create(new LocalizationOptions()), NullLoggerFactory.Instance));

    [Theory]
    [InlineData("en")]
    [InlineData("lt")]
    public void Screens_ShouldCoverTheSidebarAndBeFoundInBothLanguages(string uiLanguage)
    {
        var original = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = new CultureInfo(uiLanguage);
        try
        {
            var screens = ShopfloorNavigation.Screens(Text);

            screens.Select(s => s.Url).Should().OnlyHaveUniqueItems()
                .And.Contain(["dashboard", "workflows", "ref/work-centers", "operator-work-queue", "roletai"]);
            screens.Should().HaveCount(1 + ShopfloorNavigation.Sections.Sum(s => s.Items.Length));
            screens.Should().OnlyContain(s => !string.IsNullOrWhiteSpace(s.Title) && s.Labels.Count >= 1 && !s.Rooted);

            var english = Text["Shopfloor.WorkCenters"].Value;
            foreach (var label in screens.Single(s => s.Url == "ref/work-centers").Labels)
            {
                ScreenSearch.Match(screens, SearchText.Fold(label), 8, "/shopfloor").Select(x => x.Url)
                    .Should().Contain("/shopfloor/ref/work-centers", $"'{label}' is a menu label of that screen");
            }

            english.Should().NotBeNullOrWhiteSpace();
            CultureInfo.CurrentUICulture.Name.Should().Be(uiLanguage);
        }
        finally
        {
            CultureInfo.CurrentUICulture = original;
        }
    }
}

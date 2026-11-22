using System.Globalization;
using System.Reflection;
using System.Text.Json;
using System.Xml.Linq;
using FluentAssertions;
using LKvitai.MES.BuildingBlocks.Search;
using LKvitai.MES.Modules.Warehouse.Api.Controllers;
using LKvitai.MES.Modules.Warehouse.Domain.Entities;
using LKvitai.MES.Modules.Warehouse.Infrastructure.Persistence;
using LKvitai.MES.Modules.Warehouse.WebUI.Resources;
using LKvitai.MES.Modules.Warehouse.WebUI.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace LKvitai.MES.Tests.Warehouse.Unit;

/// <summary>
/// Portal "Search everything" (issue #375): the shared contract and matcher, the Warehouse API
/// endpoint and the Warehouse screens taken from the sidebar definition.
/// </summary>
public class UniversalSearchTests
{
    private static readonly IStringLocalizer<WarehouseResources> Text = new StringLocalizer<WarehouseResources>(
        new ResourceManagerStringLocalizerFactory(Options.Create(new LocalizationOptions()), NullLoggerFactory.Instance));

    // ── shared contract ────────────────────────────────────────────────

    [Fact]
    public void Dto_ShouldSerializeToTheCommonShape()
    {
        var web = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var json = JsonSerializer.Serialize(new SearchResultDto(SearchKinds.Item, "Title", null, "admin/items/1", 0.9), web);

        json.Should().Be("""{"kind":"item","title":"Title","subtitle":null,"url":"admin/items/1","score":0.9}""");
        JsonSerializer.Deserialize<SearchResultDto>("""{"kind":"screen","title":"T","url":"x"}""", web)
            .Should().Be(new SearchResultDto("screen", "T", null, "x"));
        new[] { SearchKinds.Order, SearchKinds.Customer, SearchKinds.Item, SearchKinds.Supplier, SearchKinds.Screen }
            .Should().Equal("order", "customer", "item", "supplier", "screen");
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData(" a ", null)]
    [InlineData(" ab ", "ab")]
    [InlineData("RM-0001", "RM-0001")]
    public void Request_ShouldRequireTwoCharacters(string? q, string? expected)
        => SearchRequest.NormalizeQuery(q).Should().Be(expected);

    [Fact]
    public void Request_ShouldRejectAbsurdlyLongQueriesAndClampTheLimit()
    {
        SearchRequest.NormalizeQuery(new string('x', SearchRequest.MaxQueryLength + 1)).Should().BeNull();
        SearchRequest.ClampLimit(null).Should().Be(SearchRequest.DefaultLimit).And.BeInRange(5, 8);
        SearchRequest.ClampLimit(0).Should().Be(1);
        SearchRequest.ClampLimit(500).Should().Be(8);
    }

    [Theory]
    [InlineData("/warehouse", "admin/items/5", "/warehouse/admin/items/5")]
    [InlineData("/warehouse/", "/admin/items/5", "/warehouse/admin/items/5")]
    [InlineData("", "dashboard", "/dashboard")]
    [InlineData(null, "", "/")]
    [InlineData("/sales", "?search=A%20B", "/sales/?search=A%20B")]
    [InlineData("/sales", "https://elsewhere.example/x", null)]
    [InlineData("/sales", "//elsewhere.example/x", null)]
    [InlineData("/sales", "javascript:alert(1)", null)]
    [InlineData("/sales", "a\\b", null)]
    [InlineData("/sales", null, null)]
    public void Relay_ShouldOnlyProduceSameOriginPaths(string? pathBase, string? url, string? expected)
        => SearchRequest.ToAbsolutePath(pathBase, url).Should().Be(expected);

    [Fact]
    public void Relay_ShouldDropMalformedHits()
    {
        var hits = ShellSearchRelay.Rebase(
            [new("item", "Ok", null, "admin/items/1"), null, new("", "No kind", null, "x"), new("item", " ", null, "x"),
             new("item", "Elsewhere", null, "https://elsewhere.example/")],
            "/warehouse");

        hits.Should().ContainSingle().Which.Url.Should().Be("/warehouse/admin/items/1");
    }

    // ── diacritics matcher ─────────────────────────────────────────────

    [Theory]
    [InlineData("Tiekėjų susiejimai", "tiekeju", true)]
    [InlineData("Tiekėjų susiejimai", "TIEKĖJŲ", true)]
    [InlineData("tiekeju susiejimai", "Tiekėjų", true)]
    [InlineData("ĄČĘĖĮŠŲŪŽ ąčęėįšųūž", "aceeisuuz ACEEISUUZ", true)]
    [InlineData("Užsakymai", "uzsak", true)]
    [InlineData("Suppliers", "tiek", false)]
    [InlineData("Suppliers", "  ", false)]
    [InlineData(null, "ab", false)]
    public void Matcher_ShouldIgnoreCaseAndLithuanianDiacritics(string? text, string query, bool expected)
        => SearchText.Contains(text, query).Should().Be(expected);

    [Fact]
    public void Matcher_ShouldFoldAndRankExactPrefixWordAndInnerMatches()
    {
        SearchText.Fold("Žalių Šilų Ąžuolas").Should().Be("zaliu silu azuolas");
        SearchText.Score("Vartotojai", "vartotojai").Should().Be(1);
        SearchText.Score("Vartotojai", "vart").Should().Be(0.9);
        SearchText.Score("Tiekėjų atitikmenys", "atitik").Should().Be(0.75);
        SearchText.Score("Supplier-Mappings", "mapp").Should().Be(0.75);
        SearchText.Score("Vartotojai", "toto").Should().Be(0.5);
        SearchText.Score("Vartotojai", "xyz").Should().Be(0);
    }

    // ── screens from the sidebar definition ────────────────────────────

    [Theory]
    [InlineData("en")]
    [InlineData("lt")]
    public void Screens_ShouldBeFoundByLithuanianAndEnglishLabelsWhateverTheUiLanguage(string uiLanguage)
    {
        var original = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = new CultureInfo(uiLanguage);
        try
        {
            var screens = WarehouseNavigation.Screens(Text);

            var byLithuanian = ScreenSearch.Match(screens, "tiekeju at", 8, "/warehouse");
            byLithuanian.Should().ContainSingle().Which.Should().BeEquivalentTo(new
            {
                Kind = SearchKinds.Screen,
                Title = uiLanguage == "lt" ? "Tiekėjų atitikmenys" : "Supplier Mappings",
                Subtitle = uiLanguage == "lt" ? "Sandėlis · Administravimas" : "Warehouse · Admin",
                Url = "/warehouse/admin/supplier-mappings",
            });
            ScreenSearch.Match(screens, "supplier map", 8, "/warehouse").Should().ContainSingle()
                .Which.Url.Should().Be("/warehouse/admin/supplier-mappings");
            ScreenSearch.Match(screens, "Vartotojai", 8, "/warehouse").Select(x => x.Url).Should().Contain("/warehouse/admin/users");
            ScreenSearch.Match(screens, "users", 8, "/warehouse").Select(x => x.Url).Should().Contain("/warehouse/admin/users");
            ScreenSearch.Match(screens, "a", 8, "/warehouse").Should().BeEmpty("one character is below the minimum");
            ScreenSearch.Match(screens, "re", 3, "/warehouse").Should().HaveCount(3, "the limit applies")
                .And.BeInDescendingOrder(x => x.Score);
            CultureInfo.CurrentUICulture.Name.Should().Be(uiLanguage, "reading the other language must not leak");
        }
        finally
        {
            CultureInfo.CurrentUICulture = original;
        }
    }

    [Fact]
    public void Navigation_ShouldKeepEveryScreenLocalizedAndLinkedOnce()
    {
        var keys = WarehouseNavigation.Sections.SelectMany(s => s.Items.Select(i => i.LabelKey).Append(s.LabelKey))
            .Append(WarehouseNavigation.Dashboard.LabelKey).ToList();
        foreach (var file in new[] { "WarehouseResources.resx", "WarehouseResources.lt.resx" })
        {
            var names = XDocument.Load(Path.Combine(WebUiRoot(), "Resources", file)).Root!.Elements("data")
                .Select(x => x.Attribute("name")!.Value).ToHashSet();
            keys.Where(k => !names.Contains(k)).Should().BeEmpty($"every menu label needs a value in {file}");
        }

        WarehouseNavigation.Sections.Should().HaveCountGreaterThan(5);
        WarehouseNavigation.Sections.Select(s => s.Key).Should().OnlyHaveUniqueItems();
        var screens = WarehouseNavigation.Screens(Text);
        screens.Select(s => s.Url).Should().NotContain(WarehouseNavigation.HangfireHref, "external links are not screens")
            .And.Contain(["dashboard", "admin/items", "admin/suppliers", "admin/settings", "reports/stock-level"]);
        screens.Should().OnlyContain(s => s.Labels.Count >= 1 && !string.IsNullOrWhiteSpace(s.Title));
    }

    // ── Warehouse API endpoint ─────────────────────────────────────────

    [Fact]
    public void Endpoint_ShouldCarryExactlyThePolicyOfTheItemAndSupplierLists()
    {
        static string? Policy(Type controller) => controller.GetMethod("GetAsync")!.GetCustomAttribute<AuthorizeAttribute>()?.Policy;

        Policy(typeof(SearchController)).Should().NotBeNullOrEmpty()
            .And.Be(Policy(typeof(ItemsController)))
            .And.Be(Policy(typeof(SuppliersController)));
        typeof(SearchController).GetCustomAttribute<AllowAnonymousAttribute>().Should().BeNull();
        typeof(SearchController).GetCustomAttribute<RouteAttribute>()!.Template.Should().Be("api/warehouse/v1/search");
    }

    [Fact]
    public async Task Endpoint_ShouldFindItemsByCodeNameBarcodeAndSupplierCode()
    {
        await using var db = CreateDb();
        Seed(db);

        (await SearchAsync(db, "rm-00")).Where(x => x.Kind == SearchKinds.Item).Select(x => x.Title)
            .Should().Equal("Sample fabric grey", "Sample fabric blue");
        var byName = (await SearchAsync(db, "FABRIC BLUE")).Should().ContainSingle().Subject;
        byName.Should().BeEquivalentTo(new { Kind = SearchKinds.Item, Title = "Sample fabric blue", Url = "admin/items/2", Subtitle = "RM-0002" });
        (await SearchAsync(db, "4770000000017")).Should().ContainSingle().Which.Subtitle.Should().Be("RM-0001 · 4770000000017");
        (await SearchAsync(db, "477000")).Should().BeEmpty("the barcode matches only as a whole, like in the item list");
        var bySupplierCode = (await SearchAsync(db, "sup-77")).Should().ContainSingle().Subject;
        bySupplierCode.Title.Should().Be("Sample fabric grey");
        bySupplierCode.Subtitle.Should().Be("RM-0001 · 4770000000017 · SUP-77-A");
        (await SearchAsync(db, "RM-0002")).First().Score.Should().Be(1);
    }

    [Fact]
    public async Task Endpoint_ShouldFindSuppliersAndLinkToTheFilteredList()
    {
        await using var db = CreateDb();
        Seed(db);

        var hit = (await SearchAsync(db, "sample supplier")).Should().ContainSingle().Subject;

        hit.Should().BeEquivalentTo(new
        {
            Kind = SearchKinds.Supplier,
            Title = "Sample supplier one",
            Subtitle = "S 01 · City · LT",
            Url = "admin/suppliers?search=S%2001",
        });
        (await SearchAsync(db, "s 01")).Should().ContainSingle(x => x.Kind == SearchKinds.Supplier);
    }

    [Fact]
    public async Task Endpoint_ShouldReturnNothingBelowTwoCharactersAndLimitEachKind()
    {
        await using var db = CreateDb();
        db.ItemCategories.Add(new ItemCategory { Id = 1, Code = "C", Name = "Cat" });
        for (var i = 1; i <= 20; i++)
        {
            db.Items.Add(NewItem(i, $"BULK-{i:00}", $"Bulk item {i}"));
            db.Suppliers.Add(new Supplier { Id = i, Code = $"BULK-S{i:00}", Name = $"Bulk supplier {i}" });
        }
        await db.SaveChangesAsync();

        (await SearchAsync(db, "b")).Should().BeEmpty();
        (await SearchAsync(db, null)).Should().BeEmpty();
        var byDefault = await SearchAsync(db, "bulk");
        byDefault.Count(x => x.Kind == SearchKinds.Item).Should().Be(SearchRequest.DefaultLimit);
        byDefault.Count(x => x.Kind == SearchKinds.Supplier).Should().Be(SearchRequest.DefaultLimit);
        (await SearchAsync(db, "bulk", 100)).Should().HaveCount(2 * SearchRequest.MaxLimit);
        byDefault.Select(x => x.Kind).Distinct().Should().Equal(SearchKinds.Item, SearchKinds.Supplier);
    }

    private static async Task<IReadOnlyList<SearchResultDto>> SearchAsync(WarehouseDbContext db, string? q, int? limit = null)
    {
        var result = await new SearchController(db).GetAsync(q, limit);
        return result.Should().BeOfType<OkObjectResult>().Subject.Value.Should().BeAssignableTo<IReadOnlyList<SearchResultDto>>().Subject;
    }

    private static void Seed(WarehouseDbContext db)
    {
        db.ItemCategories.Add(new ItemCategory { Id = 1, Code = "C", Name = "Cat" });
        var grey = NewItem(1, "RM-0001", "Sample fabric grey");
        grey.PrimaryBarcode = "4770000000017";
        db.Items.Add(grey);
        db.Items.Add(NewItem(2, "RM-0002", "Sample fabric blue"));
        db.Items.Add(NewItem(3, "BOLT-1", "Bolt"));
        db.Suppliers.Add(new Supplier { Id = 1, Code = "S 01", Name = "Sample supplier one", City = "City", Country = "LT" });
        db.Suppliers.Add(new Supplier { Id = 2, Code = "X-02", Name = "Another vendor" });
        db.SupplierItemMappings.Add(new SupplierItemMapping { Id = 1, SupplierId = 1, ItemId = 1, SupplierSKU = "SUP-77-A" });
        db.SaveChanges();
    }

    private static Item NewItem(int id, string sku, string name) => new()
    {
        Id = id,
        InternalSKU = sku,
        Name = name,
        CategoryId = 1,
        BaseUoM = "PCS",
        Status = "Active"
    };

    private static WarehouseDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<WarehouseDbContext>()
            .UseInMemoryDatabase($"universal-search-tests-{Guid.NewGuid():N}")
            .Options;
        return new WarehouseDbContext(options);
    }

    private static string WebUiRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "docs", "i18n", "README.md")))
        {
            dir = dir.Parent;
        }

        dir.Should().NotBeNull("the repository root holds docs/i18n/README.md");
        return Path.Combine(dir!.FullName, "src", "Modules", "Warehouse", "LKvitai.MES.Modules.Warehouse.WebUI");
    }
}

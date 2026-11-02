using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using LKvitai.MES.Modules.Sales.Contracts.Orders;
using LKvitai.MES.Modules.Sales.WebUI.Components;
using LKvitai.MES.Modules.Sales.WebUI.Formatting;
using LKvitai.MES.Modules.Sales.WebUI.Models;
using LKvitai.MES.Modules.Sales.WebUI.Resources;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using Xunit;

namespace LKvitai.MES.Tests.Sales.Auth;

public sealed class SalesLocalizationTests
{
    [Fact]
    public void Resource_keys_and_format_arguments_match_and_cover_source_references()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "docs/i18n/README.md"))) dir = dir.Parent;
        Assert.NotNull(dir);
        var module = Path.Combine(dir!.FullName, "src/Modules/Sales/LKvitai.MES.Modules.Sales.WebUI");
        Dictionary<string,string> Read(string name) => XDocument.Load(Path.Combine(module,"Resources",name)).Root!.Elements("data").ToDictionary(x=>x.Attribute("name")!.Value,x=>x.Element("value")!.Value);
        var en = Read("SalesResources.resx"); var lt = Read("SalesResources.lt.resx");
        Assert.Equal(en.Keys.Order(), lt.Keys.Order());
        Assert.True(en.Count > 250);
        string[] Slots(string value) => Regex.Matches(value, @"(?<!\{)\{([0-9]+)(?:[^{}]*)\}(?!\})").Select(m=>m.Groups[1].Value).Order().ToArray();
        foreach (var key in en.Keys)
        {
            Assert.False(string.IsNullOrWhiteSpace(lt[key]), key);
            Assert.Equal(Slots(en[key]), Slots(lt[key]));
        }
        foreach (var file in Directory.EnumerateFiles(module,"*",SearchOption.AllDirectories).Where(f=>(f.EndsWith(".razor")||f.EndsWith(".cshtml")||f.EndsWith(".cs"))&&!f.Contains("/obj/")&&!f.Contains("/bin/")))
            foreach (Match m in Regex.Matches(File.ReadAllText(file), "(?:L|Localizer)\\[\\s*\"([^\"]+)\""))
                Assert.True(en.ContainsKey(m.Groups[1].Value), $"Missing resource {m.Groups[1].Value}: {file}");
    }

    [Theory]
    [InlineData("en", "New order", "Customer", "Create draft and open")]
    [InlineData("lt", "Naujas užsakymas", "Klientas", "Sukurti ir atidaryti juodraštį")]
    public async Task Actual_order_entry_renders_in_the_selected_language(string language,string title,string customer,string action)
    {
        using var culture = new UiCulture(language);
        await using var services = new ServiceCollection().AddLogging().AddLocalization().AddSingleton<IJSRuntime, NoJs>().BuildServiceProvider();
        await using var renderer = new HtmlRenderer(services, services.GetRequiredService<ILoggerFactory>());
        var html = await renderer.Dispatcher.InvokeAsync(async () => WebUtility.HtmlDecode((await renderer.RenderComponentAsync<NewOrderWorkspace>()).ToHtmlString()));
        Assert.Contains(title, html); Assert.Contains(customer, html); Assert.Contains(action, html);
        Assert.Contains("VV-123", html);
    }

    [Fact]
    public void Ui_culture_preserves_formats_numbering_and_native_status_meaning()
    {
        using var culture = new UiCulture("en");
        using var services = new ServiceCollection().AddLocalization().AddLogging().BuildServiceProvider();
        var text = services.GetRequiredService<IStringLocalizer<SalesResources>>();
        var formattingCulture = CultureInfo.CurrentCulture;
        var money = OrderFormat.Money(1234.50m); var date = OrderFormat.Date(new DateOnly(2026,11,2));
        Assert.Equal("Orders",text["Common.Orders"].Value);
        // The older API assigns Entered to some full native labels. Translation
        // must preserve the actual label's meaning without changing stored codes.
        Assert.Equal("Approved",SalesStatusText.Localize(text,OrderStatusCodes.Entered,"Patvirtintas gamybai"));
        CultureInfo.CurrentUICulture = new("lt");
        Assert.Equal("Užsakymai",text["Common.Orders"].Value);
        Assert.Same(formattingCulture,CultureInfo.CurrentCulture);
        Assert.Equal(money,OrderFormat.Money(1234.50m)); Assert.Equal(date,OrderFormat.Date(new DateOnly(2026,11,2)));
        Assert.Equal("Patvirtintas",SalesStatusText.Localize(text,OrderStatusCodes.Entered,"Patvirtintas gamybai"));
        Assert.Equal("Custom native label",SalesStatusText.Localize(text,OrderStatusCodes.Entered,"Custom native label"));
        var catalog = new DemoCatalog { Series = [new() { Code = "VV" }] };
        Assert.True(DemoOrderNumber.TryNormalize("VV-123-A",2026,catalog,out var number,out _,out _));
        Assert.Equal("VV-123-26A",number);
        Assert.False(DemoOrderNumber.TryNormalize("NO-123",2026,catalog,out _,out _,out var error));
        Assert.Contains("Serijos",error);
        Assert.False(DemoOrderNumber.TryNormalize("VV-0",2026,catalog,out _,out _,out error));
        Assert.DoesNotContain("sequence",error);
    }

    private sealed class UiCulture(string language) : IDisposable
    {
        private readonly CultureInfo original = Set(language);
        private static CultureInfo Set(string language) { var original=CultureInfo.CurrentUICulture; CultureInfo.CurrentUICulture=new(language); return original; }
        public void Dispose() => CultureInfo.CurrentUICulture=original;
    }
    private sealed class NoJs : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => ValueTask.FromResult(default(TValue)!);
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier,CancellationToken cancellationToken,object?[]? args) => ValueTask.FromResult(default(TValue)!);
    }
}

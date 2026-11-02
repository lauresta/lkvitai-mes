using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using LKvitai.MES.Modules.Sales.Contracts.Orders;
using LKvitai.MES.Modules.Sales.WebUI.Components;
using LKvitai.MES.Modules.Sales.WebUI.Formatting;
using LKvitai.MES.Modules.Sales.WebUI.Models;
using LKvitai.MES.Modules.Sales.WebUI.Resources;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;
namespace LKvitai.MES.Tests.Sales.Auth;
public sealed class SalesLocalizationTests
{
    [Fact]
    public void Both_catalogues_cover_every_literal_reference_with_identical_format_arguments()
    {
        var dir=new DirectoryInfo(AppContext.BaseDirectory);
        while(dir is not null&&!File.Exists(Path.Combine(dir.FullName,"docs/i18n/README.md")))dir=dir.Parent;
        Assert.NotNull(dir);
        var module=Path.Combine(dir!.FullName,"src/Modules/Sales/LKvitai.MES.Modules.Sales.WebUI");
        Dictionary<string,string> Read(string name)=>XDocument.Load(Path.Combine(module,"Resources",name)).Root!.Elements("data").ToDictionary(x=>x.Attribute("name")!.Value,x=>x.Element("value")!.Value);
        var en=Read("SalesResources.resx");var lt=Read("SalesResources.lt.resx");
        Assert.Equal(en.Keys.Order(),lt.Keys.Order());Assert.True(en.Count>500);
        string[] Slots(string s)=>Regex.Matches(s,@"(?<!\{)\{([0-9]+)(?:[^{}]*)\}(?!\})").Select(m=>m.Groups[1].Value).Order().ToArray();
        foreach(var key in en.Keys){Assert.False(string.IsNullOrWhiteSpace(lt[key]),key);Assert.Equal(Slots(en[key]),Slots(lt[key]));}
        foreach(var file in Directory.EnumerateFiles(module,"*",SearchOption.AllDirectories).Where(f=>(f.EndsWith(".razor")||f.EndsWith(".cshtml")||f.EndsWith(".cs"))&&!f.Contains("/obj/")&&!f.Contains("/bin/")))
            foreach(Match match in Regex.Matches(File.ReadAllText(file),"(?:L|Localizer)\\[\\s*\"([^\"]+)\""))
                Assert.True(en.ContainsKey(match.Groups[1].Value),$"Missing resource {match.Groups[1].Value} in {file}");
    }
    [Fact]
    public void Ui_language_does_not_change_formatting_or_numbering_and_never_translates_legacy_data()
    {
        using var scope=new UiCulture("en");var format=CultureInfo.CurrentCulture;
        var money=OrderFormat.Money(1234.50m);var date=OrderFormat.Date(new DateOnly(2026,11,2));
        Assert.Equal("Orders",SalesTestLocalization.Text["Common.Orders"].Value);
        CultureInfo.CurrentUICulture=new("lt");
        Assert.Equal("Užsakymai",SalesTestLocalization.Text["Common.Orders"].Value);
        Assert.Same(format,CultureInfo.CurrentCulture);Assert.Equal(money,OrderFormat.Money(1234.50m));Assert.Equal(date,OrderFormat.Date(new DateOnly(2026,11,2)));
        var catalogue=new DemoCatalog{Series=[new(){Code="VV"}]};
        Assert.True(DemoOrderNumber.TryNormalize("VV-123-A",2026,catalogue,out var number,out _,out _));Assert.Equal("VV-123-26A",number);
        Assert.False(DemoOrderNumber.TryNormalize("NO-123",2026,catalogue,out _,out _,out var error));Assert.Contains("seriją",error);
        const string note="Kliento prasymu anuliuotas";Assert.Equal(note,SalesApiErrors.Translate(SalesTestLocalization.Text,"ORDER_LOCKED",note));
        var translated=SalesApiErrors.Translate(SalesTestLocalization.Text,"NOT_FOUND","Order not found.");Assert.Contains("nerastas",translated);
        Assert.StartsWith("widthMm: ",SalesApiErrors.Translate(SalesTestLocalization.Text,"DIMENSIONS","widthMm: Order not found."));
    }
    [Theory]
    [InlineData("en","Personal data restricted — GDPR","Note history")]
    [InlineData("lt","Asmens duomenys ribojami – BDAR","Pastabų istorija")]
    public async Task Actual_components_render_in_the_selected_ui_language_and_preserve_history(string language,string title,string historyTitle)
    {
        using var scope=new UiCulture(language);
        await using var services=new ServiceCollection().AddLogging().AddLocalization().BuildServiceProvider();
        await using var renderer=new HtmlRenderer(services,services.GetRequiredService<ILoggerFactory>());
        var html=await renderer.Dispatcher.InvokeAsync(async()=>WebUtility.HtmlDecode((await renderer.RenderComponentAsync<CustomerPrivacyNotice>(ParameterView.FromDictionary(new Dictionary<string,object?>{{nameof(CustomerPrivacyNotice.IsRestricted),true}}))).ToHtmlString()));
        Assert.Contains(title,html);
        var entries=new[]{new OrderNoteHistoryEntry(1,"Raw customer note VV-123",null,"SQL\\operator")};
        html=await renderer.Dispatcher.InvokeAsync(async()=>WebUtility.HtmlDecode((await renderer.RenderComponentAsync<OrderNoteHistory>(ParameterView.FromDictionary(new Dictionary<string,object?>{{nameof(OrderNoteHistory.Entries),entries}}))).ToHtmlString()));
        Assert.Contains(historyTitle,html);Assert.Contains(entries[0].Note,html);Assert.Contains(entries[0].Author!,html);
    }
    private sealed class UiCulture:IDisposable
    {
        private readonly CultureInfo original=CultureInfo.CurrentUICulture;
        public UiCulture(string language){CultureInfo.CurrentUICulture=new(language);}
        public void Dispose()=>CultureInfo.CurrentUICulture=original;
    }
}

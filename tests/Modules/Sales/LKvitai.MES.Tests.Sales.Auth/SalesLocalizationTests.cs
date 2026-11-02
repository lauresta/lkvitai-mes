using System.Globalization;
using System.Net;
using System.Reflection;
using System.Security.Claims;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using LKvitai.MES.Modules.Sales.Contracts.Orders;
using LKvitai.MES.Modules.Sales.WebUI.Components;
using LKvitai.MES.Modules.Sales.WebUI.Formatting;
using LKvitai.MES.Modules.Sales.WebUI.Models;
using LKvitai.MES.Modules.Sales.WebUI.Resources;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.JSInterop;
using LKvitai.MES.Modules.Sales.WebUI.Services;
using LKvitai.MES.BuildingBlocks.WebUI.Services;
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
        Assert.Equal("Approved",SalesStatusText.Localize(SalesTestLocalization.Text,OrderStatusCodes.Entered,"Patvirtintas gamybai"));
        CultureInfo.CurrentUICulture=new("lt");
        Assert.Equal("Užsakymai",SalesTestLocalization.Text["Common.Orders"].Value);
        Assert.Same(format,CultureInfo.CurrentCulture);Assert.Equal(money,OrderFormat.Money(1234.50m));Assert.Equal(date,OrderFormat.Date(new DateOnly(2026,11,2)));
        Assert.Equal("Patvirtintas",SalesStatusText.Localize(SalesTestLocalization.Text,OrderStatusCodes.Entered,"Patvirtintas gamybai"));
        Assert.Equal("Custom native label",SalesStatusText.Localize(SalesTestLocalization.Text,OrderStatusCodes.Entered,"Custom native label"));
        Assert.Equal("Anuliuotas",SalesStatusText.Localize(SalesTestLocalization.Text,OrderStatusCodes.Cancelled,"Anuliuotas"));
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
    [Theory]
    [InlineData("en", "New order", "Customer", "Create draft and open")]
    [InlineData("lt", "Naujas užsakymas", "Klientas", "Sukurti ir atidaryti juodraštį")]
    public async Task Actual_review_order_entry_renders_the_selected_language(string language,string title,string customer,string action)
    {
        using var culture=new UiCulture(language);
        var html=await RenderWorkspace<NewOrderWorkspace>();
        Assert.Contains(title,html);Assert.Contains(customer,html);Assert.Contains(action,html);
        Assert.Contains("VV-123",html);
    }

    [Theory]
    [InlineData("en", "Main parameters")]
    [InlineData("lt", "Pagrindiniai parametrai")]
    public async Task Product_editor_passes_the_stored_material_code_to_the_picker(string language,string heading)
    {
        using var culture=new UiCulture(language);
        var html=await RenderWorkspace<ExistingMaterialWorkspace>();
        Assert.Contains(heading,html);
        // The unavailable legacy material has no current ID/matching catalogue
        // item. Rendering the real product editor must preserve its stored code.
        Assert.Contains("value=\"LEGACY-CODE-801\"",html);
        Assert.DoesNotContain("_configuringLine.ColourCode",html);
    }

    private static async Task<string> RenderWorkspace<T>() where T:NewOrderWorkspace
    {
        var registry=new ServiceCollection().AddLogging().AddLocalization();
        registry.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        registry.AddSingleton<IJSRuntime,NoJs>();
        registry.AddSingleton<AuthenticationStateProvider,ReviewAuthentication>();
        registry.AddSingleton<NavigationManager,ReviewNavigation>();
        registry.AddHttpClient();
        registry.AddDataProtection().UseEphemeralDataProtectionProvider();
        registry.AddSalesSessionCredentials();
        registry.AddScoped<SalesOrderEntryClient>();
        registry.AddBuildVersion();
        await using var services=registry.BuildServiceProvider();
        await using var renderer=new HtmlRenderer(services,services.GetRequiredService<ILoggerFactory>());
        return await renderer.Dispatcher.InvokeAsync(async()=>WebUtility.HtmlDecode((await renderer.RenderComponentAsync<T>()).ToHtmlString()));
    }

    private sealed class ExistingMaterialWorkspace:NewOrderWorkspace
    {
        protected override Task OnInitializedAsync()
        {
            var catalogue=DemoCatalog.Load();
            var category=catalogue.Categories.First(x=>x.Name=="Roletai");
            var line=new DemoOrderLine { CategoryId=category.Id,Category=category.Name,
                TypeId=category.Types.First().Id,Name="Legacy product",ColourCode="LEGACY-CODE-801",
                WidthMm=800,HeightMm=1500,Quantity=1 };
            var order=new DemoOrderDraft { Number="VV-123-26",Date=new(2026,11,2),Due=new(2026,11,16) };
            order.Lines.Add(line);
            Set("IsLive",false);Set("_loading",false);Set("_pendingRestored",true);
            Set("Catalog",catalogue);Set("_order",order);Set("_configuringLine",line);
            return Task.CompletedTask;
        }
        private void Set(string name,object value)=>typeof(NewOrderWorkspace).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(this,value);
    }
    private sealed class ReviewAuthentication:AuthenticationStateProvider
    {
        public override Task<AuthenticationState> GetAuthenticationStateAsync()=>Task.FromResult(new AuthenticationState(
            new ClaimsPrincipal(new ClaimsIdentity([new Claim("sales_review","true")],"Review"))));
    }
    private sealed class ReviewNavigation:NavigationManager
    {
        public ReviewNavigation(){Initialize("http://localhost/sales/","http://localhost/sales/");}
        protected override void NavigateToCore(string uri,bool forceLoad){}
    }
    private sealed class NoJs:IJSRuntime
    {
        public ValueTask<T> InvokeAsync<T>(string identifier,object?[]? args)=>ValueTask.FromResult(default(T)!);
        public ValueTask<T> InvokeAsync<T>(string identifier,CancellationToken cancellationToken,object?[]? args)=>ValueTask.FromResult(default(T)!);
    }
    private sealed class UiCulture:IDisposable
    {
        private readonly CultureInfo original=CultureInfo.CurrentUICulture;
        public UiCulture(string language){CultureInfo.CurrentUICulture=new(language);}
        public void Dispose()=>CultureInfo.CurrentUICulture=original;
    }
}

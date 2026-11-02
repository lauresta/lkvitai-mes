using System.Net;
using System.Reflection;
using System.Security.Claims;
using LKvitai.MES.BuildingBlocks.WebUI.Services;
using LKvitai.MES.Modules.Sales.WebUI.Components;
using LKvitai.MES.Modules.Sales.WebUI.Formatting;
using LKvitai.MES.Modules.Sales.WebUI.Models;
using LKvitai.MES.Modules.Sales.WebUI.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using Xunit;

namespace LKvitai.MES.Tests.Sales.Auth;

public sealed class CustomerPrivacyNoticeTests
{
    private const string Title="Personal data restricted — GDPR";
    private const string Body="This order was cancelled at the customer’s request. Certain order information has been removed for privacy reasons.";

    [Fact]
    public async Task Inactive_notice_renders_no_warning_or_overlay()
    {
        var html=await Render<CustomerPrivacyNotice>(new(){[nameof(CustomerPrivacyNotice.IsRestricted)]=false});
        Assert.DoesNotContain(Title,html);Assert.DoesNotContain("customer-privacy-watermark",html);
    }

    [Fact]
    public async Task Restricted_notice_renders_approved_copy_and_a_real_keyboard_reachable_back_link()
    {
        var html=await Render<CustomerPrivacyNotice>(new(){[nameof(CustomerPrivacyNotice.IsRestricted)]=true});
        Assert.Contains(Title,html);Assert.Contains(Body,html);Assert.Contains("← Back to orders</a>",html);
        Assert.Contains("href=\"./\"",html);Assert.Contains("role=\"note\"",html);
        Assert.Contains("class=\"customer-privacy-watermark\" aria-hidden=\"true\"",html);
        Assert.DoesNotContain("tabindex=\"-1\"",html);Assert.DoesNotContain("disabled",html);
    }

    [Theory]
    [InlineData("order")][InlineData("product")][InlineData("picker")]
    public async Task Live_privacy_notice_keeps_stored_customer_products_and_amounts_visible(string mode)
    {
        var order=Order("  KLIENTO PRASYMU ANULIUOTAS  ");
        var html=await Render<PreparedWorkspace>(new(){[nameof(PreparedWorkspace.Order)]=order,[nameof(PreparedWorkspace.Mode)]=mode});
        Assert.Contains(Title,html);Assert.Contains(Body,html);
        Assert.Contains("Stored customer",html);Assert.Contains("Stored product",html);
        Assert.Contains(OrderFormat.Money(37.42m),html);
        Assert.DoesNotContain("KLIENTO PRASYMU ANULIUOTAS",html);
        Assert.Equal("  KLIENTO PRASYMU ANULIUOTAS  ",order.Note);
        Assert.Equal("Stored customer",order.Customer);Assert.Equal(37.42m,order.Total);
        Assert.Equal(37.42m,Assert.Single(order.Lines).Total);
    }

    [Theory]
    [InlineData("Cancelled")]
    [InlineData("Anuliuotas")]
    [InlineData("Kliento prasymu anuliuotas / additional note")]
    public async Task Ordinary_cancellation_or_partial_note_does_not_claim_privacy_restriction(string note)
    {
        var html=await Render<PreparedWorkspace>(new(){[nameof(PreparedWorkspace.Order)]=Order(note)});
        Assert.DoesNotContain(Title,html);Assert.Contains(note,html);Assert.Contains("Stored customer",html);
    }

    [Fact]
    public async Task A_manual_demo_note_never_claims_that_stored_information_was_removed()
    {
        var html=await Render<PreparedWorkspace>(new(){[nameof(PreparedWorkspace.Order)]=Order("Kliento prasymu anuliuotas"),[nameof(PreparedWorkspace.Live)]=false});
        Assert.DoesNotContain(Title,html);Assert.Contains("Kliento prasymu anuliuotas",html);
    }

    private static DemoOrderDraft Order(string note)
    {
        var order=new DemoOrderDraft{LegacyId=100,Number="VV-123-26",Customer="Stored customer",Store="Stored store",Note=note,Date=new(2026,11,1),Due=new(2026,11,5),Cancelled=true,CanEdit=false,WritesAllowed=false,ServerTotal=37.42m,ServerCalculated=37.42m};
        order.Lines.Add(new(){LegacyId=200,Name="Stored product",Kind="Product",Category="Roletai",Quantity=1,Price=37.42m,ServerAmount=37.42m,WidthMm=800,HeightMm=1500,CanEdit=false});
        return order;
    }

    private static async Task<string> Render<T>(Dictionary<string,object?> parameters) where T:IComponent
    {
        var js=new NoJs();
        await using var services=new ServiceCollection().AddLogging().AddSingleton<IJSRuntime>(js)
            .AddSingleton<NavigationManager>(new Navigation()).AddSingleton<AuthenticationStateProvider>(new Authentication())
            .AddSingleton<IBuildVersionAccessor>(new Build()).AddSingleton(new SalesOrderEntryClient(null!,null!,js)).BuildServiceProvider();
        await using var renderer=new HtmlRenderer(services,services.GetRequiredService<ILoggerFactory>());
        return await renderer.Dispatcher.InvokeAsync(async()=>
        {
            var rendered=await renderer.RenderComponentAsync<T>(ParameterView.FromDictionary(parameters));
            return WebUtility.HtmlDecode(rendered.ToHtmlString());
        });
    }

    // Skip transport initialization only. The real workspace render tree and
    // nested components render the saved data, including all receipt amounts.
    public sealed class PreparedWorkspace:NewOrderWorkspace
    {
        [Parameter] public DemoOrderDraft Order { get; set; }=default!;
        [Parameter] public bool Live { get; set; }=true;
        [Parameter] public string Mode { get; set; }="order";
        protected override Task OnInitializedAsync()
        {
            Set("_order",Order);Set("IsLive",Live);Set("_loading",false);Set("_pendingRestored",true);
            if(Mode=="product")Set("_configuringLine",Order.Lines[0]);
            if(Mode=="picker")Set("_selectingProduct",true);
            return Task.CompletedTask;
        }
        private void Set(string name,object value)=>typeof(NewOrderWorkspace).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(this,value);
    }
    private sealed class NoJs:IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier,object?[]? args)=>throw new InvalidOperationException("Static privacy rendering must not require JavaScript.");
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier,CancellationToken cancellationToken,object?[]? args)=>InvokeAsync<TValue>(identifier,args);
    }
    private sealed class Authentication:AuthenticationStateProvider
    { public override Task<AuthenticationState> GetAuthenticationStateAsync()=>Task.FromResult(new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()))); }
    private sealed class Navigation:NavigationManager
    { public Navigation()=>Initialize("https://example.test/sales/","https://example.test/sales/entry/100");protected override void NavigateToCore(string uri,bool forceLoad){} }
    private sealed class Build:IBuildVersionAccessor
    { public BuildVersion Get()=>new("test",null,null,null,null,null,"Test"); }
}

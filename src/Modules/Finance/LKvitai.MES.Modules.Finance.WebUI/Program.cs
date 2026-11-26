using LKvitai.MES.BuildingBlocks.ModuleStartup;
using LKvitai.MES.BuildingBlocks.PortalAuth;
using LKvitai.MES.BuildingBlocks.Search;
using LKvitai.MES.BuildingBlocks.WebUI.Localization;
using LKvitai.MES.BuildingBlocks.WebUI.Services;
using LKvitai.MES.Modules.Finance.WebUI.IncomesReport;
using LKvitai.MES.Modules.Finance.WebUI.Resources;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Localization;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.UseScaffoldSerilog("finance-webui");
builder.Services.AddScaffoldWebUiCore("FinanceApi", "FinanceApi:BaseUrl", "http://localhost:5061");
builder.Services.Configure<HttpClientFactoryOptions>("FinanceApi", options =>
    options.HttpMessageHandlerBuilderActions.Add(handler =>
        handler.PrimaryHandler = new HttpClientHandler { UseCookies = false, AllowAutoRedirect = false }));
// The initial bank page is carried through prerender hydration. Bound the
// SignalR frame while allowing the compressed snapshot of 100 payment rows.
builder.Services.AddServerSideBlazor().AddHubOptions(options => options.MaximumReceiveMessageSize = 256 * 1024);
builder.Services.AddCikadaLocalization(builder.Configuration);
builder.Services.AddPortalCookieAuthentication(builder.Environment, builder.Configuration);
builder.Services.AddAuthorization();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddHttpContextAccessor();
builder.Services.AddBuildVersion();

var app = builder.Build();

app.UsePathBase(ScaffoldModuleStartupExtensions.ResolvePathBase(app.Configuration));
app.UseCikadaRequestLocalization();
app.UsePortalSecureHosting(app.Environment);
app.UseSerilogRequestLogging();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok());
app.MapPortalLogout();
// Portal "Search everything" (#375): this module's screens for the shared top bar. The incomes report is offered
// only to the users on Finance:Incomes:AllowedUsers.
app.MapShellScreenSearch(context =>
{
    var text = context.RequestServices.GetRequiredService<IStringLocalizer<FinanceResources>>();
    var screens = new List<ScreenEntry>
    {
        ScreenEntry.FromResource(text, "bank-payments", "Bank.Title", text["Finance.Title"].Value, CikadaLocalization.LanguageCodes),
    };
    if (IncomesAccess.IsAllowed(context.User, app.Configuration))
        screens.Add(ScreenEntry.FromResource(text, "incomes", "Incomes.Title", text["Finance.Title"].Value, CikadaLocalization.LanguageCodes));
    return (IReadOnlyList<ScreenEntry>)screens;
});
app.MapCikadaCultureEndpoint();
app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

await app.RunAsync();

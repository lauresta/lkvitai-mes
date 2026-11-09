using LKvitai.MES.BuildingBlocks.ModuleStartup;
using LKvitai.MES.BuildingBlocks.PortalAuth;
using LKvitai.MES.BuildingBlocks.WebUI.Localization;
using LKvitai.MES.BuildingBlocks.WebUI.Services;
using Microsoft.Extensions.Http;
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
app.MapCikadaCultureEndpoint();
app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

await app.RunAsync();

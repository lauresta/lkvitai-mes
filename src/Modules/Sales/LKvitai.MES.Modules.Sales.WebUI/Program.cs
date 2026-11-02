using LKvitai.MES.BuildingBlocks.ModuleStartup;
using LKvitai.MES.BuildingBlocks.PortalAuth;
using LKvitai.MES.BuildingBlocks.WebUI.Services;
using LKvitai.MES.Modules.Sales.WebUI.Services;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.UseScaffoldSerilog("sales-webui");
builder.Services.AddScaffoldWebUiCore("SalesApi", "SalesApi:BaseUrl", "http://localhost:5021");

// Bump the Blazor Server SignalR receive cap so PersistentComponentState
// hydration messages aren't truncated. The framework default is 32 KB, and
// the Sales orders list serialises ~80–100 KB of state (100 rows × OrderSummaryDto
// embedded in <persist-component-state />, base64 + JSON overhead). When the
// hydration message exceeds the cap, the SignalR hub closes the connection
// with "Server returned an error on close" *before any @onclick handler is
// wired*, leaving the page rendered but completely non-interactive (filters,
// paging, dropdowns silently do nothing). 256 KB gives 2.5x headroom over the
// largest payload we currently produce; the persisted DTO itself is also slim
// (no ProductsSearch, see Pages/Index.razor).
//
// Calling AddServerSideBlazor() a second time only to chain AddHubOptions is
// the Microsoft-documented pattern; the underlying registrations are
// idempotent.
builder.Services
    .AddServerSideBlazor()
    .AddHubOptions(o => o.MaximumReceiveMessageSize = 256 * 1024);

builder.Services.AddPortalCookieAuthentication(builder.Environment, builder.Configuration);
if (string.Equals(builder.Environment.EnvironmentName, "Test", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddSalesReviewAccess(builder.Environment, builder.Configuration);
}
builder.Services.AddAuthorization();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddHttpContextAccessor();
// Surfaces APP_VERSION / GIT_SHA / BUILD_DATE env vars (injected by
// build-and-push.yml → Sales.WebUI Dockerfile ARG → ENV) into MainLayout
// so the shared topbar's "VER" badge shows the real release tag instead
// of an em-dash. Same registration Portal/Frontline call.
builder.Services.AddBuildVersion();
builder.Services.AddSalesSessionCredentials();
builder.Services.AddHttpClient("SalesApi")
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
builder.Services.AddScoped<SalesApiClient>();
builder.Services.AddScoped<SalesOrderEntryClient>();
builder.Services.AddSingleton<LKvitai.MES.Modules.Sales.Infrastructure.Stub.StubOrdersQueryService>();

var app = builder.Build();

app.UsePathBase(ScaffoldModuleStartupExtensions.ResolvePathBase(app.Configuration));
app.UsePortalSecureHosting(app.Environment);

app.UseSerilogRequestLogging();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
if (string.Equals(app.Environment.EnvironmentName, "Test", StringComparison.OrdinalIgnoreCase))
{
    app.UseSalesReviewAccess();
}
app.UseSalesAuthDiagnostics();
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok());
app.MapPortalLogout();
app.MapSalesSessionProof();
app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

await app.RunAsync();

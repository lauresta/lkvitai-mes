using LKvitai.MES.BuildingBlocks.ModuleStartup;
using LKvitai.MES.BuildingBlocks.PortalAuth;
using LKvitai.MES.Modules.Sales.WebUI.Services;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.UseScaffoldSerilog("sales-webui");
builder.Services.AddScaffoldWebUiCore("SalesApi", "SalesApi:BaseUrl", "http://localhost:5021");
builder.Services.AddPortalCookieAuthentication(builder.Environment, builder.Configuration);
builder.Services.AddAuthorization();
builder.Services.AddCascadingAuthenticationState();

// Scoped in Blazor Server = one instance per circuit. Populated in _Host.cshtml
// during the initial HTTP request, then read by components for the lifetime of
// the circuit (avoids the HttpContext-after-SignalR trap). NOT consumed from a
// DelegatingHandler because IHttpClientFactory's handler scope is shared across
// circuits and would leak one user's cookie to everyone on the same chain.
builder.Services.AddScoped<PortalAuthCookieState>();

var app = builder.Build();

app.UsePathBase(ScaffoldModuleStartupExtensions.ResolvePathBase(app.Configuration));
app.UsePortalSecureHosting(app.Environment);

app.UseSerilogRequestLogging();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok());
app.MapPortalLogout();
app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

await app.RunAsync();
